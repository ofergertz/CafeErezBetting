# CafeErezBetting — Comprehensive QA Report

**Prepared by:** QA Engineer (subagent)  
**Date:** 2025-06-07  
**Scope:** Security audit, business logic review, missing validations, error handling gaps, race conditions  
**Stack:** ASP.NET Core (.NET 8), EF Core + PostgreSQL, Redis, SignalR, BCrypt, JWT (HS256), Serilog / React 18, Vite, Zustand, React Query, react-hook-form + zod, Tailwind CSS

---

## Summary of Findings

| # | Title | Severity | Category |
|---|-------|----------|----------|
| 1 | JWT blacklist NOT checked in authentication middleware | HIGH | Security |
| 2 | OTP verify has no rate limiting — brute-forceable | HIGH | Security |
| 3 | OTP code generated with non-cryptographic `new Random()` | HIGH | Security |
| 4 | Form submission endpoints are public (no `[Authorize]`) | MEDIUM | Security |
| 5 | JWT has no issuer/audience validation | MEDIUM | Security |
| 6 | Admin login has no brute-force lockout | MEDIUM | Security |
| 7 | Token stored in `localStorage` via Zustand persist (XSS risk) | MEDIUM | Security |
| 8 | OTP rate-limit counter increment is not atomic (TOCTOU) | MEDIUM | Race Condition |
| 9 | ProductService barcode uniqueness check has TOCTOU race | MEDIUM | Race Condition |
| 10 | CustomerService IdNumber / Phone uniqueness check TOCTOU | MEDIUM | Race Condition |
| 11 | CRITICAL: `FormsPage.tsx` — `data?.forms` always undefined (API returns array) | CRITICAL | Business Logic |
| 12 | `FormsPage.tsx` — `Pending` status used in UI but does not exist in backend enum | HIGH | Business Logic |
| 13 | `FormsController.SubmitToto` — IndexOutOfRangeException when Columns is empty | HIGH | Business Logic |
| 14 | `AuthController.VerifyOtp` — token generated before `SaveChangesAsync` | MEDIUM | Error Handling |
| 15 | `FormsController` — raw `BettingForm` entity returned (leaks payload, nav props) | MEDIUM | Error Handling |
| 16 | Notify call happens after DB save — notification silently lost if Redis/SignalR fails | LOW | Error Handling |
| 17 | `BetSlip.tsx` — stake input accepts 0/negative via keyboard bypass | LOW | Missing Validation |
| 18 | `FormsController` — no server-side stake validation | LOW | Missing Validation |
| 19 | CORS — acceptable if origin list is locked down in production | INFO | Security |
| 20 | `AuditLogsController` — correctly protected with `[Authorize(Roles = "admin")]` | INFO | Positive Finding |

---

## Part 1 — Detailed Findings

---

### FINDING 1 — JWT Blacklist Not Checked in Middleware

**Severity:** HIGH  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Program.cs` (JWT middleware config) + `AuthController.cs` → `Logout()`

**Description:**  
The logout endpoint (`POST /api/auth/logout`) correctly adds the JWT's JTI claim to a Redis blacklist key (`jwt:bl:{jti}`) with a TTL matching the token's remaining lifetime. However, the JWT middleware configured in `Program.cs` never checks this blacklist before accepting a token:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(...),
            ValidateIssuer   = false,
            ValidateAudience = false,
            ClockSkew        = TimeSpan.Zero,
        };
        // ❌ No OnTokenValidated event checking Redis blacklist
    });
```

After a user calls `/api/auth/logout`, their token remains accepted by all `[Authorize]` endpoints until the token naturally expires.

**Impact:**  
- A stolen or captured JWT remains valid after the legitimate user logs out
- Session termination is effectively non-functional from a security standpoint
- An attacker who obtains a token has full access for the token's entire remaining TTL (potentially hours)

**Remediation:**  
Add an `OnTokenValidated` event handler to the `JwtBearerEvents` that reads `jwt:bl:{jti}` from Redis and calls `context.Fail()` if the key exists.

---

### FINDING 2 — OTP Verify Has No Rate Limiting (Brute-Forceable)

**Severity:** HIGH  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.Infrastructure/Services/OtpService.cs` → `VerifyOtpAsync()` + `AuthController.cs` → `VerifyOtp()`

**Description:**  
`OtpService.IsRateLimitedAsync()` and the rate limit increment only apply to the **send** operation (`SendOtpAsync`). The **verify** path (`VerifyOtpAsync`) performs no rate limiting at all. A 6-digit numeric OTP has only 1,000,000 possible values. With a 5-minute TTL (`OtpTtlSeconds = 300`), an attacker who knows a victim's phone number can:

1. Trigger one OTP send (or observe a legitimate send)
2. Immediately begin brute-forcing the verify endpoint at the network's maximum rate
3. At modest 1,000 req/s, the entire keyspace is exhausted in ~16 minutes — well within the 5-minute window for lucky guesses; with parallel requests the window is easily beaten

**Impact:**  
- Any customer account can be taken over with knowledge of the victim's phone number only
- No server-side protection prevents automated guessing
- BCrypt verification slows each individual check slightly but does not prevent the attack within the TTL

**Remediation:**  
- Implement a verify attempt counter in Redis per phone number (e.g., `otp:verify:rl:{phone}`) with a maximum of 5–10 attempts
- Lock the OTP session (mark as failed) after N failed attempts
- Return `429 Too Many Requests` when the limit is exceeded

---

### FINDING 3 — OTP Code Generated with Non-Cryptographic `new Random()`

**Severity:** HIGH  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.Infrastructure/Services/OtpService.cs` → `GenerateCode()`

**Description:**  
```csharp
private static string GenerateCode()
{
    var random = new Random();                          // ❌ Predictable seed
    return random.Next(100000, 999999).ToString();
}
```

`System.Random` uses a time-based seed by default, making it potentially predictable. Cryptographic randomness is required for security-sensitive values like OTP codes. Additionally, the range `100000–999999` excludes any code starting with `0`, slightly reducing the effective keyspace.

**Impact:**  
- An attacker who can approximate the server's time can narrow the OTP search space
- Does not conform to security standards (NIST SP 800-63B) for OTP generation

**Remediation:**  
Replace with `System.Security.Cryptography.RandomNumberGenerator`:
```csharp
private static string GenerateCode()
{
    var bytes = RandomNumberGenerator.GetBytes(4);
    var value = BitConverter.ToUInt32(bytes) % 1_000_000;
    return value.ToString("D6"); // zero-padded, covers 000000–999999
}
```

---

### FINDING 4 — Form Submission Endpoints are Public (No `[Authorize]`)

**Severity:** MEDIUM  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Controllers/FormsController.cs` → `SubmitWinner`, `SubmitToto`, `SubmitLotto`, `SubmitChance`, `Submit777`

**Description:**  
All five lottery/betting form submission endpoints lack an `[Authorize]` attribute:

```csharp
[HttpPost("winner")]   // ❌ No [Authorize]
public async Task<IActionResult> SubmitWinner(...)

[HttpPost("toto")]     // ❌ No [Authorize]
public async Task<IActionResult> SubmitToto(...)
// ... same for lotto, chance, 777
```

Any unauthenticated client can POST unlimited forms to the database. There is also no server-side rate limiting.

**Impact:**  
- Denial-of-service via database flooding — a bot can fill the `BettingForms` table with millions of records
- Admin notification system overwhelmed with bogus form notifications
- Legitimate forms buried in noise
- Storage costs and PostgreSQL performance degrade

**Remediation:**  
Add `[Authorize]` to each form submission endpoint. For customer-facing forms, `[Authorize(Roles = "customer,admin")]` or simply `[Authorize]`. Optionally add per-IP rate limiting middleware.

---

### FINDING 5 — JWT Has No Issuer or Audience Validation

**Severity:** MEDIUM  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Program.cs`

**Description:**  
```csharp
ValidateIssuer   = false,
ValidateAudience = false,
```

While the signing key is validated, disabling issuer and audience validation means:
- A JWT signed with the same secret but targeting a different service/audience would be accepted
- In a microservices or multi-tenant scenario, cross-service token replay becomes possible

**Impact:**  
- In single-service deployments the risk is low
- In any future multi-service or multi-environment deployment this becomes a HIGH risk
- Does not conform to JWT best practice (RFC 7519)

**Remediation:**  
Set `ValidateIssuer = true`, `ValidateAudience = true` and configure `ValidIssuer` / `ValidAudience` in `appsettings.json`.

---

### FINDING 6 — Admin Login Has No Brute-Force Lockout

**Severity:** MEDIUM  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Controllers/AuthController.cs` → `AdminLogin()`

**Description:**  
The admin login endpoint accepts unlimited password attempts with no lockout, CAPTCHA, or rate limiting. A BCrypt hash comparison delays each attempt by ~100–300ms, but an attacker can still attempt ~3–10 passwords/second per connection.

**Impact:**  
- Admin accounts can be brute-forced given enough time
- A 4-digit PIN equivalent password would fall in seconds
- No audit alerting on repeated failures

**Remediation:**  
- Implement per-username attempt counter in Redis with lockout after N failures (e.g., 10 attempts → 15-minute lockout)
- Or use ASP.NET Core's built-in `IPasswordHasher` with lockout support

---

### FINDING 7 — JWT Token Stored in `localStorage` (XSS Risk)

**Severity:** MEDIUM  
**Category:** Security  
**Location:** `frontend/src/store/authStore.ts` (Zustand `persist` middleware)

**Description:**  
```typescript
export const useAuthStore = create<AuthStore>()(
  persist(
    (set, get) => ({ user: null, token: null, ... }),
    { name: 'cafe-erez-auth', partialize: (state) => ({ user: state.user, token: state.token }) }
  )
)
```

The JWT is persisted to `localStorage` under the key `cafe-erez-auth`. Any XSS vulnerability (third-party library, React injection, or innerHTML usage) can read and exfiltrate this token.

**Impact:**  
- Token theft via XSS → full account takeover
- `localStorage` is accessible to all same-origin JS with no additional protection

**Remediation:**  
Store the token in an `HttpOnly` `SameSite=Strict` cookie. This requires a backend session endpoint to set the cookie. Alternatively, use `sessionStorage` (lost on tab close; reduces persistence risk) and avoid Zustand's persist middleware for sensitive data.

---

### FINDING 8 — OTP Rate Limit Counter Increment is Not Atomic (TOCTOU Race)

**Severity:** MEDIUM  
**Category:** Race Condition  
**Location:** `backend/src/CafeErezBetting.Infrastructure/Services/OtpService.cs` → `SendOtpAsync()`

**Description:**  
```csharp
var rlVal = await _cache.GetStringAsync(rlKey);   // read
var count = rlVal is null ? 1 : int.Parse(rlVal) + 1;
await _cache.SetStringAsync(rlKey, count.ToString(), ...); // write
```

This is a read-increment-write pattern on Redis without atomic locking. Two concurrent OTP send requests for the same phone number can both read `count=0`, both compute `count=1`, and both write `1` — effectively bypassing the rate limit entirely with concurrent requests.

**Impact:**  
- The effective rate limit can be up to `MaxSendsPerWindow × concurrent_request_count` sends per window
- Attacker can trigger unlimited OTP SMS sends, causing SMS abuse costs

**Remediation:**  
Use `IDatabase.StringIncrementAsync()` (StackExchange.Redis atomic increment) or a Lua script instead of read-modify-write. With `IDistributedCache` abstraction, implement this via `StackExchangeRedisCache`'s underlying connection.

---

### FINDING 9 — ProductService Barcode Uniqueness Check Has TOCTOU Race

**Severity:** MEDIUM  
**Category:** Race Condition  
**Location:** `backend/src/CafeErezBetting.Infrastructure/Services/ProductService.cs` → `CreateAsync()` and `UpdateAsync()`

**Description:**  
```csharp
var exists = await db.Products.AnyAsync(p => p.Barcode == dto.Barcode.Trim());
if (exists) throw new InvalidOperationException("Barcode already in use.");
// ← Another concurrent request can pass here before either commits
db.Products.Add(product);
await db.SaveChangesAsync();
```

Two concurrent POST requests with the same barcode can both pass the `AnyAsync` check before either reaches `SaveChangesAsync`, resulting in duplicate barcode records in the database.

**Impact:**  
- Barcode uniqueness constraint violated in the application layer
- If there is no database-level `UNIQUE` constraint on the barcode column, duplicate barcodes will be silently inserted
- Barcode scan lookups would return ambiguous results

**Remediation:**  
- Add a `UNIQUE` constraint on `Products.Barcode` in the database (migration) — this is the definitive fix and surfaces as a DB exception to be caught and returned as 409
- Alternatively wrap in a serializable transaction, but a DB constraint is simpler and more reliable

---

### FINDING 10 — CustomerService IdNumber / Phone Uniqueness Check Has TOCTOU Race

**Severity:** MEDIUM  
**Category:** Race Condition  
**Location:** `backend/src/CafeErezBetting.Infrastructure/Services/CustomerService.cs` → `CreateAsync()`

**Description:**  
Same pattern as Finding 9, applied to IdNumber and Phone:
```csharp
if (await db.Customers.AnyAsync(c => c.IdNumber == dto.IdNumber, ct))
    throw new ArgumentException("A customer with this IdNumber already exists.");
if (await db.Customers.AnyAsync(c => c.Phone == dto.Phone, ct))
    throw new ArgumentException("A customer with this Phone already exists.");
// ← Race window here
db.Customers.Add(customer);
await db.SaveChangesAsync(ct);
```

**Impact:**  
- Duplicate customers with the same Israeli ID number can be created in concurrent registration scenarios
- Phone uniqueness violated — two customers could hold the same phone number

**Remediation:**  
Add `UNIQUE` constraints on `Customers.IdNumber` and `Customers.Phone` at the database level and handle `DbUpdateException` (unique constraint violation) gracefully.

---

### FINDING 11 — CRITICAL: `FormsPage.tsx` Shows Empty List (API Shape Mismatch)

**Severity:** CRITICAL  
**Category:** Business Logic Bug  
**Location:** `frontend/src/pages/Admin/Forms/FormsPage.tsx` → `const forms = data?.forms ?? []`

**Description:**  
The React Query call fetches from `/api/forms` and types the response as `{ forms: BettingForm[] }`:

```typescript
const { data, isLoading, isError } = useQuery({
    queryKey: ['forms', ...],
    queryFn: () => api.get<{ forms: BettingForm[] }>(`/api/forms${queryString}`),
})
// ...
const forms = data?.forms ?? []  // ← BUG: data IS the array, not a wrapper
```

However, `FormsController.GetForms` returns the array directly:
```csharp
return Ok(forms);  // returns BettingForm[], NOT { forms: BettingForm[] }
```

`data` will be the `BettingForm[]` array itself. `data.forms` will always be `undefined`, so `forms` is always `[]`. The admin forms table is permanently empty regardless of what the backend returns.

**Impact:**  
- The admin forms management screen is completely non-functional
- Admins cannot see, acknowledge, approve, or mark-sent any betting forms
- Core business workflow is broken

**Remediation:**  
Change the query to correctly type the response:
```typescript
queryFn: () => api.get<BettingForm[]>(`/api/forms${queryString}`),
// and:
const forms = data ?? []
```

Or change the backend to wrap its response: `return Ok(new { forms = forms })`.

---

### FINDING 12 — `Pending` Status Used in Frontend Does Not Exist in Backend Enum

**Severity:** HIGH  
**Category:** Business Logic Bug  
**Location:** `frontend/src/pages/Admin/Forms/FormsPage.tsx` + backend `FormStatus` enum

**Description:**  
`FormsPage.tsx` defines the `BettingForm` type with `status: 'Pending' | 'Received' | 'Approved' | 'Sent'` and the `StatusBadge` component handles `'Pending'`. The `Acknowledge (קבלתי)` button is enabled only when `status === 'Pending'` (i.e., `!isReceived` where `isReceived = status === 'Received' || ...`).

The backend `FormStatus` enum only has: `Received`, `Approved`, `Sent`. All newly created forms start with `Status = FormStatus.Received`. Since `Received` is the initial state, `isReceived` is always `true` for all real forms, making the **Acknowledge button permanently disabled** for every form the backend ever returns.

**Impact:**  
- The "קבלתי" (Acknowledge) button is always greyed out and non-clickable
- The intended acknowledgement workflow step is completely unusable
- Admin cannot progress forms through the first step of the lifecycle

**Remediation:**  
Either:
- Add a `Pending` status to the backend `FormStatus` enum (and set it as the initial status when forms are created)
- Or remove the Acknowledge step from the UI and treat `Received` as the initial actionable state, adjusting the button logic accordingly

---

### FINDING 13 — `FormsController.SubmitToto` — NullReferenceException When `Columns` is Empty

**Severity:** HIGH  
**Category:** Business Logic Bug  
**Location:** `backend/src/CafeErezBetting.API/Controllers/FormsController.cs` → `SubmitToto()`

**Description:**  
```csharp
LotteryValidationService.ValidateToto(dto, expectedMatchCount: dto.Columns[0].Picks.Count);
```

If `dto.Columns` is `null`, this throws a `NullReferenceException`. If `dto.Columns` is an empty list `[]`, this throws an `IndexOutOfRangeException`. Neither is caught by the surrounding try/catch (which only catches `ArgumentException`). The exception propagates as an unhandled 500 Internal Server Error instead of a proper 400 Bad Request.

**Impact:**  
- Sending `{ "Columns": [] }` or `{ "Columns": null }` causes a 500 error and may log a stack trace
- Could be exploited for error disclosure / server fingerprinting
- Disrupts API reliability

**Remediation:**  
Add a guard before accessing `dto.Columns[0]`:
```csharp
if (dto.Columns == null || dto.Columns.Count == 0)
    return BadRequest(new { message = "At least one column is required." });
```

---

### FINDING 14 — `AuthController.VerifyOtp` — Token Issued Before `SaveChangesAsync`

**Severity:** MEDIUM  
**Category:** Error Handling  
**Location:** `backend/src/CafeErezBetting.API/Controllers/AuthController.cs` → `VerifyOtp()`

**Description:**  
```csharp
var token = _jwt.GenerateCustomerToken(customer);  // ← token generated using in-memory customer.Id

_db.AuditLogs.Add(new AuditLog { ... });
await _db.SaveChangesAsync();  // ← if this fails, customer was never persisted

return Ok(new { token, user = new { id = customer.Id, ... } });
```

For a new customer (not found by phone), the customer entity is added to the DbContext but not yet saved when `GenerateCustomerToken` is called. The token embeds `customer.Id` (the EF-generated GUID, which exists in memory but not in the database). If `SaveChangesAsync` fails (DB timeout, constraint violation, connection error), the method returns an exception response — but if by any timing quirk the client receives the token before the exception propagates, it holds a token for a non-existent database record.

In practice, `SaveChangesAsync` failure will return a 500 before the `Ok(...)` call, so this is unlikely to cause real harm. However, the token is generated on a record that may not be persisted, which is architecturally incorrect.

**Impact:**  
- Low practical risk in the happy path
- In edge cases (partial commit scenarios), a token for a non-existent customer could be issued
- Incorrect sequencing of token generation vs persistence

**Remediation:**  
Move `_jwt.GenerateCustomerToken(customer)` to after `await _db.SaveChangesAsync()`.

---

### FINDING 15 — `FormsController.GetForms` Returns Raw Entities (Data Leakage)

**Severity:** MEDIUM  
**Category:** Error Handling / Data Leakage  
**Location:** `backend/src/CafeErezBetting.API/Controllers/FormsController.cs` → `GetForms()`

**Description:**  
`formsService.GetAllFormsAsync()` returns entities that include the `Payload` field (`JsonDocument`) and any navigation properties (e.g., `Customer`). Serializing `JsonDocument` with `System.Text.Json` can produce unexpected output and may expose the full raw betting form payload to the admin client.

**Impact:**  
- Full form payload (bets, picks, personal data) included in the list response — potentially more data than the admin UI needs
- `JsonDocument` serialization is not guaranteed to be stable across EF materialization

**Remediation:**  
Return a DTO projection from `GetAllFormsAsync` that includes only the fields the admin UI requires (id, type, customerId, customer name, status, timestamps).

---

### FINDING 16 — Form Save + Notification Not Atomic (Reliability Gap)

**Severity:** LOW  
**Category:** Error Handling  
**Location:** `backend/src/CafeErezBetting.API/Controllers/FormsController.cs` (Toto, Lotto, Chance, 777 endpoints)

**Description:**  
All four lottery form submission endpoints follow this pattern:
```csharp
db.BettingForms.Add(form);
await db.SaveChangesAsync(ct);      // ← form is persisted in DB
await notifier.NotifyNewFormAsync(...);  // ← if this throws, form is already saved but admin never notified
```

(Note: `SubmitWinnerFormAsync` in `FormsService` likely has the same pattern.) If the Redis/SignalR notification call fails after the DB commit, the form is permanently saved but the admin receives no real-time notification of its existence.

**Impact:**  
- Forms silently accumulate without admin awareness in Redis/SignalR failure scenarios
- Not a data loss issue — forms are in the DB and will appear on next page refresh
- Admin must manually refresh to discover orphaned forms

**Remediation:**  
Wrap the save + notify in a try/catch and log notification failures without surfacing as errors to the client. Consider an outbox pattern for guaranteed delivery.

---

### FINDING 17 — `BetSlip.tsx` Stake Input Accepts Zero/Negative (Client-Side Only)

**Severity:** LOW  
**Category:** Missing Validation  
**Location:** `frontend/src/pages/Winner/BetSlip.tsx` → stake `<input>`

**Description:**  
```tsx
<input type="number" min="1" step="1" value={stake || ''} onChange={e => setStake(Math.max(0, parseFloat(e.target.value) || 0))} />
```

The `min="1"` attribute is HTML-side only and can be trivially bypassed by typing directly. `Math.max(0, ...)` allows `0` as a stake value. A `stake=0` bet is submitted to the server (the `canSubmit` check requires `stake > 0` which prevents UI submission — but this is a client-side guard only).

**Impact:**  
- User can type `0` or negative numbers and briefly see `0 ₪` potential win
- If `canSubmit` guard is bypassed (e.g., programmatic submission), a zero-stake form reaches the server

**Remediation:**  
Change to `Math.max(1, ...)` and add server-side validation in `FormsController` to reject `stake <= 0`.

---

### FINDING 18 — No Server-Side Stake Validation on Form Submission

**Severity:** LOW  
**Category:** Missing Validation  
**Location:** `backend/src/CafeErezBetting.API/Controllers/FormsController.cs` → `SubmitWinner()` (and `FormsService.SubmitWinnerFormAsync`)

**Description:**  
The winner form DTO (`SubmitWinnerFormDto`) presumably contains a `Stake` field. Neither `FormsController` nor `FormsService` validates that the stake is positive before saving. A crafted API request can submit a form with `stake: -9999` or `stake: 0`.

**Impact:**  
- Negative or zero-stake forms saved to the database
- Potential for incorrect financial calculations if stake is used downstream

**Remediation:**  
Add validation: `if (dto.Stake <= 0) return BadRequest(new { message = "Stake must be positive." });`

---

### FINDING 19 — CORS Configuration (Informational)

**Severity:** INFO  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Program.cs`

**Description:**  
CORS is configured with specific origins from `Frontend:Url` config + `AllowCredentials()`. This is correctly locked to configured origins and not using a wildcard. **No issue in production** as long as the `Frontend:Url` config value is not a wildcard and is kept up to date in deployment. Worth verifying that `appsettings.Production.json` does not have `*` as the origin.

---

### FINDING 20 — AuditLogsController Correctly Protected (Positive Finding)

**Severity:** INFO  
**Category:** Security  
**Location:** `backend/src/CafeErezBetting.API/Controllers/AuditLogsController.cs`

**Description:**  
The audit log controller is correctly decorated with `[Authorize(Roles = "admin")]` at the class level, ensuring all endpoints require admin authentication. No finding.

---

## Part 2 — Playwright E2E Test Coverage Summary

### Files Created

| File | Location | Tests |
|------|----------|-------|
| `playwright.config.ts` | `frontend/playwright.config.ts` | Config |
| `auth.spec.ts` | `frontend/e2e/auth.spec.ts` | 6 tests |
| `winner.spec.ts` | `frontend/e2e/winner.spec.ts` | 6 tests |
| `lotto.spec.ts` | `frontend/e2e/lotto.spec.ts` | 7 tests |
| `admin-forms.spec.ts` | `frontend/e2e/admin-forms.spec.ts` | 6 tests |
| `store.spec.ts` | `frontend/e2e/store.spec.ts` | 8 tests |

**Total: 33 E2E tests**

### Coverage Highlights

- All API calls mocked with `page.route()` — no backend dependency
- Auth state injected via `addInitScript` + `localStorage`
- SignalR connections aborted in `beforeEach` equivalent setup
- `admin-forms.spec.ts` includes a clearly labelled test that will **intentionally fail** until Finding 11 (data?.forms bug) is fixed — this serves as a regression guard
- Store barcode scan test handles both camera-UI and text-input scanner variants gracefully

### How to Run

```bash
cd /data/.openclaw/workspace/CafeErezBetting/frontend
npm run test:e2e
```

To run with UI explorer:
```bash
npx playwright test --ui
```

---

## Priority Remediation Order

1. **[CRITICAL]** Fix `FormsPage.tsx` — change `data?.forms ?? []` to `data ?? []` (Finding 11)
2. **[HIGH]** Add JWT blacklist check in middleware `OnTokenValidated` event (Finding 1)
3. **[HIGH]** Add OTP verify rate limiting (Finding 2)
4. **[HIGH]** Replace `new Random()` with `RandomNumberGenerator` for OTP generation (Finding 3)
5. **[HIGH]** Resolve `Pending` / `Received` status mismatch — add `Pending` to backend or fix frontend logic (Finding 12)
6. **[HIGH]** Guard `dto.Columns[0]` access in `SubmitToto` (Finding 13)
7. **[MEDIUM]** Add `[Authorize]` to all form submission endpoints (Finding 4)
8. **[MEDIUM]** Add admin login rate limiting / lockout (Finding 6)
9. **[MEDIUM]** Add database-level UNIQUE constraints on barcode, IdNumber, Phone (Findings 9, 10)
10. **[MEDIUM]** Move token generation after `SaveChangesAsync` in `VerifyOtp` (Finding 14)
11. **[MEDIUM]** Enable JWT issuer + audience validation (Finding 5)
12. **[MEDIUM]** Use atomic Redis increment for OTP rate limit (Finding 8)
13. **[LOW]** Add server-side stake validation (Finding 18)
14. **[LOW]** Return form DTOs instead of raw entities from `GetForms` (Finding 15)

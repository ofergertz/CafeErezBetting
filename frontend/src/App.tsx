import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { useAuthStore } from '@/store/authStore'
import Layout from '@/components/Layout/Layout'
import WinnerPage   from '@/pages/Winner/WinnerPage'
import TotoPage     from '@/pages/Toto/TotoPage'
import LottoPage    from '@/pages/Lotto/LottoPage'
import ChancePage   from '@/pages/Chance/ChancePage'
import Lucky777Page from '@/pages/Lucky777/Lucky777Page'
import StorePage    from '@/pages/Store/StorePage'
import CustomersPage from '@/pages/Admin/Customers/CustomersPage'
import FormsPage    from '@/pages/Admin/Forms/FormsPage'
import KioskPage    from '@/pages/Admin/Kiosk/KioskPage'
import AuditLogsPage from '@/pages/Admin/AuditLogsPage'
import LoginPage    from '@/pages/LoginPage'

function ProtectedAdminRoute({ children }: { children: React.ReactNode }) {
  const isAdmin = useAuthStore((s) => s.isAdmin())
  return isAdmin ? <>{children}</> : <Navigate to="/login" replace />
}

export default function App() {
  return (
    <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<Layout />}>
          <Route index element={<Navigate to="/winner" replace />} />
          <Route path="winner"  element={<WinnerPage />} />
          <Route path="toto"    element={<TotoPage />} />
          <Route path="lotto"   element={<LottoPage />} />
          <Route path="chance"  element={<ChancePage />} />
          <Route path="777"     element={<Lucky777Page />} />
          <Route path="store"   element={<StorePage />} />
          <Route path="customers" element={
            <ProtectedAdminRoute><CustomersPage /></ProtectedAdminRoute>
          } />
          <Route path="forms" element={
            <ProtectedAdminRoute><FormsPage /></ProtectedAdminRoute>
          } />
          <Route path="kiosk" element={
            <ProtectedAdminRoute><KioskPage /></ProtectedAdminRoute>
          } />
          <Route path="audit-logs" element={
            <ProtectedAdminRoute><AuditLogsPage /></ProtectedAdminRoute>
          } />
        </Route>
        <Route path="*" element={<Navigate to="/winner" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

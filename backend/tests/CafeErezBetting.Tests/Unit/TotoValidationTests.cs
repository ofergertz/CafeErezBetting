using CafeErezBetting.Core.DTOs;
using CafeErezBetting.Core.Services;
using FluentAssertions;
using Xunit;

namespace CafeErezBetting.Tests.Unit;

public class TotoValidationTests
{
    private static SubmitTotoFormDto BuildDto(int columnCount, int picksPerColumn)
    {
        var columns = new List<TotoColumnDto>();
        for (int c = 0; c < columnCount; c++)
        {
            var picks = new Dictionary<string, string>();
            for (int i = 0; i < picksPerColumn; i++)
                picks[$"match_{i}"] = "1";
            columns.Add(new TotoColumnDto(picks));
        }
        return new SubmitTotoFormDto("round1", columns, null);
    }

    [Fact]
    public void ValidateToto_ZeroPicks_ThrowsArgumentException()
    {
        // Column exists but has zero picks — expectedMatchCount derived from Picks.Count will be 0
        var dto = BuildDto(columnCount: 1, picksPerColumn: 0);

        var act = () => LotteryValidationService.ValidateToto(dto, expectedMatchCount: dto.Columns[0].Picks.Count);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one match pick*");
    }

    [Fact]
    public void ValidateToto_ValidPicks_DoesNotThrow()
    {
        var dto = BuildDto(columnCount: 2, picksPerColumn: 3);

        var act = () => LotteryValidationService.ValidateToto(dto, expectedMatchCount: dto.Columns[0].Picks.Count);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateToto_NoColumns_ThrowsArgumentException()
    {
        var dto = new SubmitTotoFormDto("round1", [], null);

        var act = () => LotteryValidationService.ValidateToto(dto, expectedMatchCount: 3);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*At least one column*");
    }

    [Fact]
    public void ValidateToto_TooManyColumns_ThrowsArgumentException()
    {
        var dto = BuildDto(columnCount: 15, picksPerColumn: 3);

        var act = () => LotteryValidationService.ValidateToto(dto, expectedMatchCount: dto.Columns[0].Picks.Count);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Maximum 14*");
    }

    [Fact]
    public void ValidateToto_InvalidPickValue_ThrowsArgumentException()
    {
        var column = new TotoColumnDto(new Dictionary<string, string> { ["m0"] = "3" });
        var dto = new SubmitTotoFormDto("round1", [column], null);

        var act = () => LotteryValidationService.ValidateToto(dto, expectedMatchCount: 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid pick*");
    }
}

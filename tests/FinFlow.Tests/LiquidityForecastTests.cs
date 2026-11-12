using FinFlow.Domain.Calculators;
using FinFlow.Domain.Reference;
using Xunit;

namespace FinFlow.Tests;

/// <summary>Прогноз ликвидности: формулы (1.11)–(1.13), таблица 1.5 записки.</summary>
public class LiquidityForecastTests
{
    private static LiquidityForecastResult Forecast() =>
        LiquidityCalculator.Forecast(ModelBankData.LiquidityDemo, ModelBankData.LiquidityDemoDays());

    [Fact]
    public void Third_day_has_outflow_9300_and_cash_7500()
    {
        var day = Forecast().Days[2];

        Assert.Equal(9300m, day.Outflow);
        Assert.Equal(7500m, day.Cash);
    }

    /// <summary>(1.13) Gap_3 = max(0; 10 000 − 7 500) = 2 500 тыс. р.</summary>
    [Fact]
    public void Third_day_gap_is_2500()
    {
        var day = Forecast().Days[2];

        Assert.Equal(2500m, day.Gap);
    }

    /// <summary>Разрыв сохраняется на четвёртый день (1 000 тыс. р.) и закрывается на пятый.</summary>
    [Fact]
    public void Gap_profile_matches_table_1_5()
    {
        var forecast = Forecast();

        Assert.Equal(1000m, forecast.Days[3].Gap);
        Assert.Equal(0m, forecast.Days[4].Gap);
        Assert.Equal(2, forecast.GapDays);
        Assert.Equal(2500m, forecast.MaxGap);
        Assert.Equal(7500m, forecast.MinCash);
    }

    /// <summary>Первый разрыв возникает на третий день горизонта.</summary>
    [Fact]
    public void First_gap_is_on_third_day()
    {
        var forecast = Forecast();

        Assert.Equal(new DateOnly(2026, 11, 18), forecast.FirstGapDate);
    }
}

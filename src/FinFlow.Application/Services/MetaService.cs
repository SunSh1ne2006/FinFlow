using FinFlow.Application.Abstractions;
using FinFlow.Contracts;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;

namespace FinFlow.Application.Services;

/// <summary>Справочные данные для клиентов: роли, валюты, лимиты, пороги (экран «Главная панель»).</summary>
public class MetaService
{
    private readonly IClock _clock;
    private readonly ParameterService _parameters;

    public MetaService(IClock clock, ParameterService parameters)
    {
        _clock = clock;
        _parameters = parameters;
    }

    public async Task<MetaDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        return new MetaDto
        {
            Version = "1.0.0",
            ServerTime = _clock.UtcNow,
            Roles = ModelBankData.Roles.Select(r => new RoleInfoDto
            {
                Code = (int)r.Code,
                CodeName = r.Code.ToString(),
                Name = r.Name,
                Client = r.Client,
                Description = r.Description
            }).ToList(),
            Currencies = new[] { "BYN", "USD", "EUR", "RUB", "CNY" },
            OperationKinds = Enum.GetNames(typeof(OperationKind)),
            HoldSources = new[] { "AisIdo", "Bank", "CardAuthorization", "Booking" },
            DepositTypes = new[] { "Demand", "Term", "Conditional" },
            OperatorLimit = await _parameters.GetValueAsync(ParameterService.OperatorLimit, today, ModelBankData.OperatorLimit, cancellationToken),
            ControllerLimit = await _parameters.GetValueAsync(ParameterService.ControllerLimit, today, ModelBankData.ControllerLimit, cancellationToken),
            AmlThreshold = await _parameters.GetValueAsync(ParameterService.AmlThreshold, today, ModelBankData.AmlThreshold, cancellationToken),
            RvsrNote = $"РВСР на ноябрь 2026 г.: вклады в BYN сроком до 3 лет – {ModelBankData.RvsrByn1To3Years:0.00} %, свыше 3 лет – {ModelBankData.RvsrBynOver3Years:0.00} % годовых."
        };
    }
}

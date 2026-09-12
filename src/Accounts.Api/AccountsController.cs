using Accounts.Application;
using Bank.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.Api;

[ApiController]
[Route("cuentas")]
[Route("api/cuentas")]
public sealed class AccountsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, Guid? cliente = null, int page = 1, int size = 50) =>
        Ok(await service.List(cliente, page, size, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.Get(id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(AccountInput input, CancellationToken ct)
    {
        var c = await service.Create(input, ct);
        return Created($"/cuentas/{c.CuentaId}", c);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, AccountUpdate input, CancellationToken ct) =>
        Ok(await service.Patch(id, new(input.TipoCuenta, input.Estado), ct));
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, AccountPatch input, CancellationToken ct) => Ok(await service.Patch(id, input, ct));
}

[ApiController]
[Route("movimientos")]
[Route("api/movimientos")]
public sealed class MovementsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, Guid? cuenta = null, int page = 1, int size = 50) =>
        Ok(await service.Movements(cuenta, page, size, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetMovement(id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(MovementInput input, CancellationToken ct,
        [FromHeader(Name = "Idempotency-Key")] Guid? idempotencyKey = null)
    {
        var m = await service.Register(input, idempotencyKey, ct);
        return Created($"/movimientos/{m.MovimientoId}", m);
    }
    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, MovementUpdate input, CancellationToken ct) => Ok(await service.Correct(id, input, ct));
}

[ApiController]
[Route("reportes")]
[Route("api/reportes")]
public sealed class ReportsController(AccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid cliente, CancellationToken ct, string? fecha = null,
        DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (fecha is not null)
        {
            var range = fecha.Split(',');
            if (range.Length != 2 || !DateOnly.TryParseExact(range[0], "yyyy-MM-dd", out var from) ||
                !DateOnly.TryParseExact(range[1], "yyyy-MM-dd", out var to))
                throw new BusinessException("fecha debe tener formato yyyy-MM-dd,yyyy-MM-dd.");
            desde = from; hasta = to;
        }
        if (cliente == Guid.Empty || desde is null || hasta is null)
            throw new BusinessException("Indique cliente y rango fecha, o desde/hasta.");
        return Ok(await service.Report(cliente, desde.Value, hasta.Value, ct));
    }
}

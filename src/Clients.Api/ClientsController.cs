using Clients.Application;
using Microsoft.AspNetCore.Mvc;

namespace Clients.Api;

[ApiController]
[Route("clientes")]
[Route("api/clientes")]
public sealed class ClientsController(ClientService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int size = 50) => Ok(await service.List(page, size, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.Get(id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(ClientInput input, CancellationToken ct)
    {
        var c = await service.Create(input, ct);
        return Created($"/clientes/{c.ClienteId}", c);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Put(Guid id, ClientInput input, CancellationToken ct) => Ok(await service.Replace(id, input, ct));
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Patch(Guid id, ClientPatch input, CancellationToken ct) => Ok(await service.Patch(id, input, ct));
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.Delete(id, ct);
        return NoContent();
    }
}

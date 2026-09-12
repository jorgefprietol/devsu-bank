using Bank.Contracts;
using Clients.Domain;

namespace Clients.Application;

public sealed record ClientInput(string Nombre, string Genero, int Edad, string Identificacion,
    string Direccion, string Telefono, string Contrasena, bool Estado = true);
public sealed record ClientPatch(string? Nombre, string? Genero, int? Edad, string? Identificacion,
    string? Direccion, string? Telefono, string? Contrasena, bool? Estado);
public sealed record ClientView(Guid ClienteId, string Nombre, string Genero, int Edad,
    string Identificacion, string Direccion, string Telefono, bool Estado)
{
    public static ClientView From(Cliente c) => new(c.Id, c.Nombre, c.Genero, c.Edad,
        c.Identificacion, c.Direccion, c.Telefono, c.Estado);
}

public interface IClientRepository
{
    Task<Cliente?> Find(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Cliente>> List(int page, int size, CancellationToken ct);
    void Add(Cliente cliente);
    Task SaveWithEvent(Cliente cliente, CancellationToken ct);
}
public interface IPasswordHasher { string Hash(string password); }

public sealed class ClientService(IClientRepository repository, IPasswordHasher hasher)
{
    public async Task<ClientView> Get(Guid id, CancellationToken ct) => ClientView.From(await Find(id, ct));
    public async Task<IReadOnlyList<ClientView>> List(int page, int size, CancellationToken ct)
    {
        if (page < 1 || size is < 1 or > 100) throw new BusinessException("Página >= 1 y tamaño entre 1 y 100.");
        return (await repository.List(page, size, ct)).Select(ClientView.From).ToList();
    }
    public async Task<ClientView> Create(ClientInput input, CancellationToken ct)
    {
        var c = Cliente.Create(input.Nombre, input.Genero, input.Edad, input.Identificacion,
            input.Direccion, input.Telefono, Hash(input.Contrasena), input.Estado);
        repository.Add(c);
        await repository.SaveWithEvent(c, ct);
        return ClientView.From(c);
    }
    public Task<ClientView> Replace(Guid id, ClientInput input, CancellationToken ct) => Patch(id,
        new(input.Nombre, input.Genero, input.Edad, input.Identificacion, input.Direccion,
            input.Telefono, input.Contrasena, input.Estado), ct);
    public async Task<ClientView> Patch(Guid id, ClientPatch input, CancellationToken ct)
    {
        var c = await Find(id, ct);
        c.Update(input.Nombre ?? c.Nombre, input.Genero ?? c.Genero, input.Edad ?? c.Edad,
            input.Identificacion ?? c.Identificacion, input.Direccion ?? c.Direccion,
            input.Telefono ?? c.Telefono, input.Contrasena is null ? null : Hash(input.Contrasena),
            input.Estado ?? c.Estado);
        await repository.SaveWithEvent(c, ct);
        return ClientView.From(c);
    }
    public async Task Delete(Guid id, CancellationToken ct)
    {
        var c = await Find(id, ct);
        c.Delete();
        await repository.SaveWithEvent(c, ct);
    }
    private string Hash(string password) => hasher.Hash(Rules.Required(password, "Contraseña", 128));
    private async Task<Cliente> Find(Guid id, CancellationToken ct) =>
        await repository.Find(id, ct) ?? throw new BusinessException("Cliente no encontrado.", 404);
}

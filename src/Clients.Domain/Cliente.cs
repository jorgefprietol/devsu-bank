using Bank.Contracts;

namespace Clients.Domain;

public abstract class Persona
{
    public Guid Id { get; protected set; }
    public string Nombre { get; protected set; } = "";
    public string Genero { get; protected set; } = "";
    public int Edad { get; protected set; }
    public string Identificacion { get; protected set; } = "";
    public string Direccion { get; protected set; } = "";
    public string Telefono { get; protected set; } = "";
}

public sealed class Cliente : Persona
{
    private Cliente() { }
    public string PasswordHash { get; private set; } = "";
    public bool Estado { get; private set; }
    public bool Eliminado { get; private set; }
    public long Version { get; private set; }

    public static Cliente Create(string nombre, string genero, int edad, string identificacion,
        string direccion, string telefono, string passwordHash, bool estado)
    {
        var cliente = new Cliente { Id = Guid.NewGuid() };
        cliente.Update(nombre, genero, edad, identificacion, direccion, telefono, passwordHash, estado);
        return cliente;
    }

    public void Update(string nombre, string genero, int edad, string identificacion,
        string direccion, string telefono, string? passwordHash, bool estado)
    {
        if (Eliminado) throw new BusinessException("Cliente eliminado.", 404);
        if (edad is < 0 or > 130) throw new BusinessException("Edad debe estar entre 0 y 130.");
        Nombre = Rules.Required(nombre, "Nombre");
        Genero = Rules.Required(genero, "Género", 30);
        Edad = edad;
        Identificacion = Rules.Required(identificacion, "Identificación", 30);
        Direccion = Rules.Required(direccion, "Dirección", 250);
        Telefono = Rules.Required(telefono, "Teléfono", 30);
        if (passwordHash is not null) PasswordHash = Rules.Required(passwordHash, "Contraseña", 500);
        Estado = estado;
        Version++;
    }

    public void Delete()
    {
        Eliminado = true;
        Estado = false;
        Version++;
    }
}

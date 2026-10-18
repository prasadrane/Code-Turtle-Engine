namespace MultiProject.ProjB;

public class ServiceB
{
    public string Greet() => new MultiProject.ProjA.ServiceA().Greet();
}

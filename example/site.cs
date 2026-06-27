using System.Collections.Generic;
namespace Site;

public static class Greeter
{
    public static string Welcome() => "it works!";

    public static List<string> Names = new List<string>();

    public static void AddName(string name)
    {
        Names.Add(name);
    }
}

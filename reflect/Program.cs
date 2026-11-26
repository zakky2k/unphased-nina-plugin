using Mono.Cecil;
var dll = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NINA\Plugins\3.0.0\Unphased Night Planner\UnphasedNina.dll");
var asm = AssemblyDefinition.ReadAssembly(dll);
Console.WriteLine("ver=" + asm.Name.Version);
foreach (var ca in asm.MainModule.CustomAttributes) {
    var n = ca.AttributeType.Name;
    if (n == "TargetFrameworkAttribute" || (n == "AssemblyMetadataAttribute" && (string)ca.ConstructorArguments[0].Value == "MinimumApplicationVersion"))
        Console.WriteLine(n + ": " + ca.ConstructorArguments[0].Value + (ca.ConstructorArguments.Count>1 ? " = " + ca.ConstructorArguments[1].Value : ""));
}

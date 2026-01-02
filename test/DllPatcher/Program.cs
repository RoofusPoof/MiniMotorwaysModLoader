using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DllPatcher
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: DllPatcher <game_managed_folder> [modloader_dll_path]");
                Console.WriteLine("Example: DllPatcher \"/path/to/Mini Motorways_Data/Managed\" \"/path/to/ModLoader.dll\"");
                return 1;
            }

            string gamePath = args[0];
            string modLoaderPath = args.Length > 1 ? args[1] : null;
            
            string appDllPath = Path.Combine(gamePath, "App.dll");
            string backupPath = Path.Combine(gamePath, "App.dll.backup");
            
            Console.WriteLine("MMW PATCHER");
            Console.WriteLine($"Game Path: {gamePath}");
            Console.WriteLine($"Target DLL: {appDllPath}");
            
            if (!Directory.Exists(gamePath))
            {
                Console.WriteLine($"ERROR: Game directory not found: {gamePath}");
                return 1;
            }
            
            if (!File.Exists(backupPath))
            {
                Console.WriteLine("Creating backup...");
                File.Copy(appDllPath, backupPath);
                Console.WriteLine($"Backup saved: {backupPath}");
            }
            else
            {
                Console.WriteLine("Backup already exists, skipping.");
            }
            
            Console.WriteLine("\nLoading App.dll...");
            
            // Tell Cecil where to find Unity DLLs
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(gamePath);  // Add the Managed folder
            
            var readerParams = new ReaderParameters 
            { 
                ReadWrite = true,
                AssemblyResolver = resolver
            };
            using (var assembly = AssemblyDefinition.ReadAssembly(appDllPath, readerParams))
            {
                // Find the App type
                var appType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "App" && t.Namespace == "");
                if (appType == null)
                {
                    Console.WriteLine("ERROR: Could not find App class!");
                    return 1;
                }
                
                // Find the Start() method
                var startMethod = appType.Methods.FirstOrDefault(m => m.Name == "Start" && m.Parameters.Count == 0);
                if (startMethod == null)
                {
                    Console.WriteLine("ERROR: Could not find App.Start() method!");
                    return;
                }
                
                Console.WriteLine($"Found App.Start() at {startMethod.FullName}");
                
                // Check if already patched
                var firstInstruction = startMethod.Body.Instructions.FirstOrDefault();
                if (firstInstruction != null && firstInstruction.OpCode == OpCodes.Ldstr)
                {
                    var strValue = firstInstruction.Operand as string;
                    if (strValue != null && (strValue.Contains("MODLOADER") || strValue.Contains("ModLoader")))
                    {
                        Console.WriteLine("\n>>> App.dll is already patched! <<<");
                        return;
                    }
                }
                
                // Inject mod loader call
                Console.WriteLine("\nInjecting mod loader call...");
                int instructionsAdded = InjectModLoaderCall(assembly.MainModule, startMethod, modLoaderPath);
                Console.WriteLine($"  OK - {instructionsAdded} IL instructions injected");
                
                // Patch BuildingPlacer.GetBaseWeightForTile for EndlessExpansion
                Console.WriteLine("\nPatching BuildingPlacer.GetBaseWeightForTile...");
                int buildingPlacerPatched = PatchBuildingPlacer(assembly.MainModule);
                if (buildingPlacerPatched > 0)
                {
                    Console.WriteLine($"  OK - {buildingPlacerPatched} IL instructions injected for BuildingPlacer");
                }
                else
                {
                    Console.WriteLine("  SKIPPED - BuildingPlacer patch not applied (may already be patched)");
                }
                
                // Save the patched assembly
                Console.WriteLine("\nSaving patched App.dll...");
                assembly.Write();
                
                Console.WriteLine("OK");
                Console.WriteLine("OK - Patch applied");
                Console.WriteLine("\nNext steps:");
                Console.WriteLine("1. Copy ModLoader.dll to the Managed folder");
                Console.WriteLine("2. Copy EndlessExpansion.dll to the Mods folder");
                Console.WriteLine("3. Launch the game");
                Console.WriteLine("4. Check Unity Player.log for mod output");
            }
        }
        
        static int InjectModLoaderCall(ModuleDefinition module, MethodDefinition targetMethod, string modLoaderPath)
        {
            var il = targetMethod.Body.GetILProcessor();
            var firstInstruction = targetMethod.Body.Instructions[0];
            int instructionCount = 0;
            
            // Get mscorlib from the Managed folder (not from our .NET 8 runtime!)
            var mscorlibRef = module.AssemblyReferences.FirstOrDefault(r => r.Name == "mscorlib");
            if (mscorlibRef == null)
            {
                Console.WriteLine("ERROR: Could not find mscorlib reference in App.dll!");
                return 0;
            }
            
            var mscorlib = module.AssemblyResolver.Resolve(mscorlibRef);
            
            // Import types from the game's mscorlib (.NET 4.0)
            var stringType = module.ImportReference(mscorlib.MainModule.TypeSystem.String);
            var objectType = module.ImportReference(mscorlib.MainModule.TypeSystem.Object);
            
            // Manual import logic for mscorlib types
            var typeType = ResolveType(mscorlib, "System.Type");
            var assemblyType = ResolveType(mscorlib, "System.Reflection.Assembly");
            var methodBaseType = ResolveType(mscorlib, "System.Reflection.MethodBase");
            
            if (typeType == null || assemblyType == null)
            {
                Console.WriteLine("ERROR: Could not resolve System.Type or System.Reflection.Assembly");
                return 0;
            }
            
            var loadFromMethod = assemblyType.Methods.FirstOrDefault(m => m.Name == "LoadFrom" && m.Parameters.Count == 1);
            var getTypeMethod = assemblyType.Methods.FirstOrDefault(m => m.Name == "GetType" && m.Parameters.Count == 1);
            var getMethodMethod = typeType.Methods.FirstOrDefault(m => m.Name == "GetMethod" && m.Parameters.Count == 1);
            var invokeMethod = methodBaseType?.Methods.FirstOrDefault(m => m.Name == "Invoke" && m.Parameters.Count == 2);
            
            if (loadFromMethod == null)
            {
                Console.WriteLine("ERROR: Could not find Assembly.LoadFrom(string). Available methods:");
                foreach (var m in assemblyType.Methods.Where(m => m.Name == "LoadFrom"))
                    Console.WriteLine($" - {m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name))})");
            }
            
            if (loadFromMethod == null || getTypeMethod == null || getMethodMethod == null || invokeMethod == null)
            {
                Console.WriteLine($"Missing methods: LoadFrom={loadFromMethod!=null}, GetType={getTypeMethod!=null}, GetMethod={getMethodMethod!=null}, Invoke={invokeMethod!=null}");
                return 0;
            }
            
            // Import references
            var loadFromRef = module.ImportReference(loadFromMethod);
            var getTypeRef = module.ImportReference(getTypeMethod);
            var getMethodRef = module.ImportReference(getMethodMethod);
            var invokeRef = module.ImportReference(invokeMethod);
            
            // Add Unity Debug.Log first
            var unityDebugType = module.Types.FirstOrDefault(t => t.FullName == "UnityEngine.Debug");
            if (unityDebugType == null)
            {
                var coreModule = module.AssemblyResolver.Resolve(new AssemblyNameReference("UnityEngine.CoreModule", null));
                unityDebugType = coreModule?.MainModule.Types.FirstOrDefault(t => t.Name == "Debug" && t.Namespace == "UnityEngine");
            }
            
            MethodReference debugLog = null;
            if (unityDebugType != null)
            {
                var logMethod = unityDebugType.Methods.FirstOrDefault(m => m.Name == "Log" && m.Parameters.Count == 1);
                if (logMethod != null)
                {
                    debugLog = module.ImportReference(logMethod);
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "[MODLOADER] App.Start() reached!"));
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, debugLog));
                    instructionCount += 2;
                }
            }
            
            var exceptionType = mscorlib.MainModule.Types.FirstOrDefault(t => t.FullName == "System.Exception");
            var exceptionTypeRef = module.ImportReference(exceptionType);
            
            var tryStart = il.Create(OpCodes.Nop);
            il.InsertBefore(firstInstruction, tryStart);
            instructionCount++;
            
            // provided mod loader path or default to Managed/ModLoader.dll
            string modDllPath = !string.IsNullOrEmpty(modLoaderPath) 
                ? modLoaderPath 
                : Path.Combine(Path.GetDirectoryName(module.FileName), "ModLoader.dll");
                
            Console.WriteLine($"Using ModLoader path: {modDllPath}");
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, modDllPath));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, loadFromRef));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "MiniMotorwaysModLoader.ModLoaderEntry"));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, getTypeRef));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "Main"));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, getMethodRef));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, invokeRef));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Pop));
            instructionCount += 10;
            
            var leaveInstruction = il.Create(OpCodes.Leave, firstInstruction);
            il.InsertBefore(firstInstruction, leaveInstruction);
            instructionCount++;
            
            //add variable at END to not shift existing indices
            int exVarIndex = targetMethod.Body.Variables.Count;
            targetMethod.Body.Variables.Add(new VariableDefinition(exceptionTypeRef));
            var catchStart = il.Create(OpCodes.Stloc, targetMethod.Body.Variables[exVarIndex]);
            il.InsertBefore(firstInstruction, catchStart);
            instructionCount++;
            
            if (debugLog != null)
            {
                var toStringMethod = mscorlib.MainModule.Types.FirstOrDefault(t => t.FullName == "System.Object")?.Methods.FirstOrDefault(m => m.Name == "ToString" && m.Parameters.Count == 0);
                var concatMethod = mscorlib.MainModule.Types.FirstOrDefault(t => t.FullName == "System.String")?.Methods.FirstOrDefault(m => m.Name == "Concat" && m.Parameters.Count == 2);
                
                if (toStringMethod != null && concatMethod != null)
                {
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, "[MODLOADER ERROR] "));
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldloc, targetMethod.Body.Variables[exVarIndex]));
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Callvirt, module.ImportReference(toStringMethod)));
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, module.ImportReference(concatMethod)));
                    il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, debugLog));
                    instructionCount += 5;
                }
            }
            
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Leave, firstInstruction));
            instructionCount++;
            
            // Add exception handler
            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                TryStart = tryStart,
                TryEnd = catchStart,
                HandlerStart = catchStart,
                HandlerEnd = firstInstruction,
                CatchType = exceptionTypeRef
            };
            targetMethod.Body.ExceptionHandlers.Add(handler);
            
            return instructionCount;
        }
        static TypeDefinition ResolveType(AssemblyDefinition assembly, string fullName)
        {
            return assembly.MainModule.Types.FirstOrDefault(t => t.FullName == fullName) 
                ?? assembly.MainModule.ExportedTypes.Select(et => et.Resolve()).FirstOrDefault(t => t?.FullName == fullName);
        }
    }
}

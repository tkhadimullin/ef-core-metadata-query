using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace ef_metadata_query
{
    public static class Extensions
    {
        public static MethodBuilder OverrideOnConfiguring(this TypeBuilder tb)
        {
            MethodBuilder onConfiguringMethod = tb.DefineMethod("OnConfiguring",
                MethodAttributes.Public
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot
                | MethodAttributes.Virtual,
                CallingConventions.HasThis,
                null,
                new[] { typeof(DbContextOptionsBuilder) });

            // the easiest method to pick will be .UseInMemoryDatabase(this DbContextOptionsBuilder optionsBuilder, string databaseName, Action<InMemoryDbContextOptionsBuilder> inMemoryOptionsAction = null)
            // but since constructing generic delegate seems a bit too much effort we'd rather filter everything else out
            var useInMemoryDatabaseMethodSignature = typeof(InMemoryDbContextOptionsExtensions)
                .GetMethods()
                .Where(m => m.Name == "UseInMemoryDatabase")
                .Where(m => m.GetParameters().Length == 3)
                .Where(m => m.GetParameters().Select(p => p.ParameterType).Contains(typeof(DbContextOptionsBuilder)))
                .Where(m => m.GetParameters().Select(p => p.ParameterType).Contains(typeof(string)))
                .Single();
            
            // emits the equivalent of optionsBuilder.UseInMemoryDatabase("test");
            var gen = onConfiguringMethod.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Ldstr, Guid.NewGuid().ToString());
            gen.Emit(OpCodes.Ldnull);
            gen.Emit(OpCodes.Call, useInMemoryDatabaseMethodSignature);
            gen.Emit(OpCodes.Pop);
            gen.Emit(OpCodes.Ret);

            return onConfiguringMethod;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            // load assembly under test
            var assembly = Assembly.LoadFrom(@"your_path_here\OnlineShoppingStore\bin\Debug\netcoreapp3.1\OnlineShoppingStore.dll");
            var contextType = assembly.GetTypes().First(d => d.Name == "OnlineStoreDbContext");

            // create yet another assembly that will hold our dynamically generated type
            var typeBuilder = AssemblyBuilder
                                .DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.RunAndCollect)
                                .DefineDynamicModule(Guid.NewGuid() + ".dll")
                                .DefineType("InheritedDbContext", TypeAttributes.Public, contextType); // make new type inherit from DbContext under test!

            // this is the key here! now our dummy implementation will kick in!
            var onConfiguringMethod = typeBuilder.OverrideOnConfiguring();
            typeBuilder.DefineMethodOverride(onConfiguringMethod, typeof(DbContext).GetMethod("OnConfiguring", BindingFlags.Instance | BindingFlags.NonPublic));
            
            var inheritedDbContext = typeBuilder.CreateType(); // enough config, let's get the type and roll with it

            // instantiate inheritedDbContext with default OnConfiguring implementation
            var context = Activator.CreateInstance(inheritedDbContext) as DbContext; // instantiate your context. this will effectively build your model, so you must have all required EF references in your project
            var p = context?.Model.FindEntityType(assembly.GetTypes().First(d => d.Name == "Product")); // get the type from loaded assembly
            
            //query the as-built model
            //var p = ctx.Model.FindEntityType("OnlineStoreDbContext.Product"); // querying model by type name also works, but you'd need to correctly qualify your type names
            var pk = p.FindPrimaryKey().Properties.First().Name; // your PK property name as built by EF model
            
            Console.WriteLine(pk);
        }
    }
}

using System.Reflection;
using System.Text;

namespace Shared.Union
{
    public class UnionDeclFromPrototype<TProto> : UnionDecl
        where TProto : struct
    {
        public UnionDeclFromPrototype(string typeName)
            : base(typeName)
        {
            var type = typeof(TProto);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Union content:");
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Append(new SubStructInfo(field.FieldType.Name, field.Name));
                sb.AppendFormat("Size of {0}: {1}\n", field.FieldType.Name, System.Runtime.InteropServices.Marshal.SizeOf(field.FieldType));
            }
            Log.i(sb.ToString());

            foreach (var i in type.GetInterfaces())
            {
                AppendInterface(i);
            }
        }
    }
}

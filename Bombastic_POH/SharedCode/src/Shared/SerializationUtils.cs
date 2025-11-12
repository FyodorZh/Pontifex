using Geom2d;
using Geom3d;
namespace Shared
{
    public static class SerializationUtils
    {
        public static StorageFolder SerializeVector3(string name, Vector3 value)
        {
            var valueFolder = new StorageFolder(name);
            valueFolder.AddItem(new StorageFloat("X", value.x));
            valueFolder.AddItem(new StorageFloat("Y", value.y));
            valueFolder.AddItem(new StorageFloat("Z", value.z));
            return valueFolder;
        }

        public static Vector3 DeserializeVector3(StorageFolder folder)
        {
            return DeserializeVector3(folder, Vector3.zero);
        }

        public static Vector3 DeserializeVector3(StorageFolder folder, Vector3 defValue)
        {
            return folder != null ? new Vector3(folder.GetItemAsFloat("X"), folder.GetItemAsFloat("Y"), folder.GetItemAsFloat("Z")) : defValue;
        }

        public static StorageFolder SerializeVector(string name, Vector value)
        {
            var valueFolder = name != null ? new StorageFolder(name) : new StorageFolder();
            valueFolder.AddItem(new StorageFloat("X", value.x));
            valueFolder.AddItem(new StorageFloat("Y", value.y));
            return valueFolder;
        }

        public static StorageItem SerializeRotation(string name, Rotation rotation)
        {
            var valueFolder = name != null ? new StorageFloat(name, rotation.AngleDegrees) : new StorageFloat(rotation.AngleDegrees);
            return valueFolder;
        }

        public static Vector DeserializeVector(StorageFolder folder)
        {
            return DeserializeVector(folder, Vector.Zero);
        }

        public static Vector DeserializeVector(StorageFolder folder, Vector defValue)
        {
            return folder != null ? new Vector(folder.GetItemAsFloat("X"), folder.GetItemAsFloat("Y")) : defValue;
        }

        public static Rotation DeserializeRotation(StorageItem rotation)
        {
            return DeserializeRotation(rotation, Rotation.Identity);
        }

        public static Rotation DeserializeRotation(StorageItem rotation, Rotation defValue)
        {
            return rotation != null ? Rotation.FromDegree(rotation.asFloat()) : defValue;
        }
    }
}

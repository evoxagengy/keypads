using System.Runtime.InteropServices;
using System.Text;
using SwitchKeypad.Core.Models;
namespace SwitchKeypad.Windows.RawInput;
public static class DeviceMetadata
{
    public static void Enrich(DeviceDefinition device)
    {
        var path=device.DevicePath;if(string.IsNullOrEmpty(path)||path.Length<4)return;
        var parts=path[4..].Split('#');if(parts.Length<3)return;
        var instance=string.Join("\\",parts.Take(3));
        if(CM_Locate_DevNode(out uint node,instance,0)!=0)return;
        var original=node;
        var friendly=Read(node,"A45C254E-DF1C-4EFD-8020-67D146A850E0",14);
        var description=Read(node,"A45C254E-DF1C-4EFD-8020-67D146A850E0",2);
        device.Manufacturer=Read(node,"A45C254E-DF1C-4EFD-8020-67D146A850E0",13);
        for(int i=0;i<4;i++){
            var bus=Read(node,"540B947E-8B40-45BC-A8A2-6A0B894CBDA2",4);
            if(!string.IsNullOrWhiteSpace(bus)){device.Product=bus;break;}
            if(CM_Get_Parent(out uint parent,node,0)!=0)break;
            var parentId=new StringBuilder(1024);
            if(CM_Get_Device_ID(parent,parentId,parentId.Capacity,0)!=0 || device.Vid is null || !parentId.ToString().Contains("VID_"+device.Vid,StringComparison.OrdinalIgnoreCase))break;
            node=parent;
        }
        device.FriendlyName=friendly??device.Product??description??"Teclado HID";
        device.Location=Read(original,"A45C254E-DF1C-4EFD-8020-67D146A850E0",15);device.InstanceId=instance;
    }
    private static string? Read(uint node,string format,uint property){
        var key=new PropertyKey{Format=new Guid(format),Id=property};var bytes=new byte[4096];uint length=(uint)bytes.Length;
        if(CM_Get_DevNode_Property(node,ref key,out uint type,bytes,ref length,0)!=0||type!=0x12)return null;
        return Encoding.Unicode.GetString(bytes,0,(int)length).TrimEnd('\0');
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey{public Guid Format;public uint Id;}
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode,EntryPoint="CM_Locate_DevNodeW")] private static extern uint CM_Locate_DevNode(out uint node,string id,uint flags);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode,EntryPoint="CM_Get_Device_IDW")] private static extern uint CM_Get_Device_ID(uint node,StringBuilder buffer,int length,uint flags);
    [DllImport("cfgmgr32.dll",EntryPoint="CM_Get_DevNode_PropertyW")] private static extern uint CM_Get_DevNode_Property(uint node,ref PropertyKey key,out uint type,byte[] buffer,ref uint size,uint flags);
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

public partial class Blocks {
 [StructLayout(LayoutKind.Sequential)]
 struct ChooseColorOptions {
  public uint Size;public IntPtr Owner,Instance;public uint Color;public IntPtr CustomColors;public uint Flags;public IntPtr Data,Hook,Template;
 }
 [DllImport("comdlg32.dll",EntryPoint="ChooseColorW",ExactSpelling=true)]
 [return:MarshalAs(UnmanagedType.Bool)]
 static extern bool ChooseNativeColor(ref ChooseColorOptions options);
 [DllImport("comdlg32.dll",ExactSpelling=true)] static extern uint CommDlgExtendedError();
 readonly int[] customPickerColors=new int[16];
 string PickWindowsColor(Window owner,string hex) {
  var color=(Color)ColorConverter.ConvertFromString(hex);IntPtr custom=Marshal.AllocHGlobal(16*sizeof(int));
  try {
   Marshal.Copy(customPickerColors,0,custom,16);
   var options=new ChooseColorOptions {Size=(uint)Marshal.SizeOf<ChooseColorOptions>(),Owner=new WindowInteropHelper(owner).EnsureHandle(),Color=(uint)(color.R|(color.G<<8)|(color.B<<16)),CustomColors=custom,Flags=0x1|0x2};
   bool accepted=ChooseNativeColor(ref options);Marshal.Copy(custom,customPickerColors,0,16);
   if(!accepted){uint error=CommDlgExtendedError();if(error!=0)throw new InvalidOperationException("Windowsの色選択ダイアログを開けませんでした（"+error+"）。");return null;}
   return $"#{options.Color&255:X2}{(options.Color>>8)&255:X2}{(options.Color>>16)&255:X2}";
  }finally {Marshal.FreeHGlobal(custom);}
 }
 void ChooseProjectColor(string project) {
  try {string color=PickWindowsColor(this,ColorFor(project));if(color!=null)SetProjectColor(project,color);}
  catch(Exception ex){MessageBox.Show(this,ex.Message,"色を変更できません");}
 }
 void SetProjectColor(string project,string color) {
  if(color==null)state.FixedColors.Remove(ProjectColorKey(project));else state.FixedColors[ProjectColorKey(project)]=color;
  colorSignature="";Save();RefreshColors();Populate();Render();
 }
}


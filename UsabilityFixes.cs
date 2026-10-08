using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
public partial class Blocks {
 bool DropWithinSelection(Point point){if(!rangeStart.HasValue||point.X<Gutter)return false;int day=(int)((point.X-Gutter)/DayWidth);double minute=point.Y/Hour*60;return day>=0&&day<DisplayDayCount&&DisplayStart.AddDays(day)==rangeStart.Value.Date&&minute>=rangeStart.Value.TimeOfDay.TotalMinutes&&minute<rangeStart.Value.TimeOfDay.TotalMinutes+rangeMinutes;}
 void OpenKimaiBrowser(){try{string url=service?.BaseUrl??settings.Url;if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"&&uri.Scheme!="http")throw new ArgumentException("有効なKimai URLを設定してください。");Process.Start(new ProcessStartInfo {FileName=uri.AbsoluteUri,UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,SafeError(ex),"Kimaiを開けません");}}
 void RestoreCalendarFocus(){Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(()=>{if(OwnedWindows.Count==0){Keyboard.ClearFocus();board.Focus();}}));}
}

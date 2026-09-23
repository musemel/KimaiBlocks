using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public sealed class CalendarRules {
 public int[] DaysOff {get;set;}=new[]{0,6};
 public string Holidays {get;set;}="";
 public string Breaks {get;set;}="12:00-13:00";
 public bool Shade {get;set;}=true;
 public bool BlockInput {get;set;}
 public List<(int Start,int End)> Intervals() {
  var result=new List<(int,int)>();
  foreach(string line in (Breaks??"").Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries)) {
   var pair=line.Trim().Split('-');
   if(pair.Length!=2||!Minute(pair[0],out int begin)||!Minute(pair[1],out int end)||begin>=end)throw new ArgumentException("休み時間は 12:00-13:00 の形式で指定してください（5分単位、同日内、終了は24:00まで）。");
   result.Add((begin,end));
  }
  return result;
 }
 static bool Minute(string text,out int minute) {
  minute=0;text=text.Trim();if(text=="24:00"){minute=1440;return true;}
  if(!TimeSpan.TryParseExact(text,"hh\\:mm",CultureInfo.InvariantCulture,out var time)||time.TotalHours>=24||time.Minutes%5!=0)return false;
  minute=(int)time.TotalMinutes;return true;
 }
 public HashSet<DateTime> HolidayDates() {
  var dates=new HashSet<DateTime>();
  foreach(string line in (Holidays??"").Split(new[]{'\r','\n',','},StringSplitOptions.RemoveEmptyEntries)) {
   if(!DateTime.TryParseExact(line.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var day))throw new ArgumentException("休業日は yyyy-MM-dd の形式で指定してください。");dates.Add(day.Date);
  }
  return dates;
 }
 public bool IsDayOff(DateTime day)=> (DaysOff??Array.Empty<int>()).Contains((int)day.DayOfWeek)||HolidayDates().Contains(day.Date);
 public static void Tests() {
  var rules=new CalendarRules();var monday=new DateTime(2026,9,21);
  if(rules.Intersects(monday.AddHours(11),60)||rules.Intersects(monday.AddHours(13),5))throw new Exception("Break boundary is inclusive");
  if(!rules.Intersects(monday.AddHours(11).AddMinutes(55),10)||!rules.Intersects(monday.AddDays(5).AddHours(9),5))throw new Exception("Forbidden interval missed");
  rules.Holidays="2026-09-22";if(!rules.Intersects(monday.AddDays(1).AddHours(9),5)||rules.Intersects(monday.AddHours(23),60))throw new Exception("Holiday boundary failed");
  rules.Breaks="12:00-13:00\n18:00-18:15";if(!rules.Intersects(monday.AddHours(18),5))throw new Exception("Multiple breaks failed");
  rules.Breaks="12:01-13:00";try {rules.Intervals();throw new Exception("Invalid interval accepted");}catch(ArgumentException){}
 }
 public bool Intersects(DateTime start,int minutes) {
  DateTime end=start.AddMinutes(minutes);
  for(DateTime day=start.Date;day<end;day=day.AddDays(1)) {
   if(IsDayOff(day))return true;
   foreach(var span in Intervals())if(start<day.AddMinutes(span.End)&&end>day.AddMinutes(span.Start))return true;
  }
  return false;
 }
}
public partial class Blocks {
 CalendarRules Rules=>settings.Calendar??(settings.Calendar=new CalendarRules());
 void DrawUnavailable() {
  if(!Rules.Shade)return;
  var intervals=Rules.Intervals();var holidays=Rules.HolidayDates();
  for(int d=0;d<DisplayDayCount;d++) {
   DateTime date=week.AddDays(d);
   bool off=(Rules.DaysOff??Array.Empty<int>()).Contains((int)date.DayOfWeek)||holidays.Contains(date);
   foreach(var range in off?new List<(int Start,int End)>{(0,1440)}:intervals) {
    var shade=new Border {Width=DayWidth,Height=(range.End-range.Start)/60.0*Hour,Background=BrushOf(off?"#D2D9E2":"#DFE4EB"),IsHitTestVisible=false};
    Canvas.SetLeft(shade,Gutter+d*DayWidth);Canvas.SetTop(shade,range.Start/60.0*Hour);board.Children.Add(shade);
   }
  }
 }
 bool AcceptSchedule(Entry entry,Entry before) {
  if(!Rules.BlockInput||before!=null&&before.Start==entry.Start&&before.Minutes==entry.Minutes||!Rules.Intersects(entry.Start,entry.Minutes))return true;
  MessageBox.Show(this,"休日または休み時間に重なるため入力できません。\n時間帯を変更するか、「休日・休み時間」で入力禁止を解除してください。","入力できない時間帯",MessageBoxButton.OK,MessageBoxImage.Information);return false;
 }
 static void RestoreEntry(Entry target,Entry original) {
  target.Project=original.Project;target.Activity=original.Activity;target.ProjectId=original.ProjectId;target.ActivityId=original.ActivityId;target.Start=original.Start;target.Minutes=original.Minutes;target.Note=original.Note;target.Billable=original.Billable;
 }
 void CalendarDialog() {
  if(communicating)return;
  var w=new Window {Title="休日・休み時間",Owner=this,Width=510,Height=650,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  panel.Children.Add(Label("毎週の休日",15));var days=new WrapPanel();var checks=new List<CheckBox>();
  string[] labels={"日","月","火","水","木","金","土"};for(int d=0;d<7;d++){var c=new CheckBox {Content=labels[d],IsChecked=(Rules.DaysOff??Array.Empty<int>()).Contains(d),Margin=new Thickness(8)};checks.Add(c);days.Children.Add(c);}panel.Children.Add(days);
  panel.Children.Add(Label("追加の休業日（1行に1日、例: 2026-12-31）",12));var holidays=new TextBox {Text=Rules.Holidays,Height=90,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(holidays);
  panel.Children.Add(Label("毎日の休み時間（1行に1区間、例: 12:00-13:00）",12));var breaks=new TextBox {Text=Rules.Breaks,Height=90,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(breaks);
  var shade=new CheckBox {Content="休日・休み時間を暗色で表示",IsChecked=Rules.Shade,Margin=new Thickness(4,16,4,8)};
  var block=new CheckBox {Content="休日・休み時間への入力を禁止",IsChecked=Rules.BlockInput,Margin=new Thickness(4,8,4,12)};panel.Children.Add(shade);panel.Children.Add(block);
  var note=Label("Kimaiユーザーの時刻で適用します。祝日は自動取得しません。\n入力禁止は新規作成・移動・時間変更に適用します。\n既存の実績や保存待ちの変更は削除しません。",12);note.TextWrapping=TextWrapping.Wrap;panel.Children.Add(note);
  panel.Children.Add(ButtonOf("保存",()=>{
   var updated=new CalendarRules {DaysOff=checks.Select((c,i)=>c.IsChecked==true?i:-1).Where(i=>i>=0).ToArray(),Holidays=holidays.Text,Breaks=breaks.Text,Shade=shade.IsChecked==true,BlockInput=block.IsChecked==true};
   var original=settings.Calendar;
   try {updated.Intervals();updated.HolidayDates();settings.Calendar=updated;StoreSettings();Render();w.Close();}
   catch(Exception ex){settings.Calendar=original;MessageBox.Show(w,SafeError(ex),"設定を保存できません");}
  }));w.ShowDialog();
 }
}

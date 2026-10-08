using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

public sealed class MemberChoice {
 public int Id {get;set;}public string Name {get;set;}public bool Selected {get;set;}
 public override string ToString()=>Name;
}
public static class MemberReports {
 public static ReportSnapshot Filter(ReportSnapshot data,HashSet<int> users)=>new ReportSnapshot {Records=data.Records.Where(r=>users==null||users.Contains(r.UserId)).ToList(),Users=data.Users.Where(u=>users==null||users.Contains(u.Id)).ToList(),Notice=data.Notice,Timezone=data.Timezone,From=data.From,Through=data.Through,Retrieved=data.Retrieved};
 // Endpoints filter by start time; display slices of all records in the retrieved snapshot.
 public static IEnumerable<(ReportRecord Record,DateTime Start,DateTime End)> Slices(IEnumerable<ReportRecord> entries,DateTime day) {
  foreach(var e in entries){DateTime end=e.End??e.Begin.AddMinutes(5);if(e.Begin>=day.AddDays(1)||end<=day)continue;yield return (e,e.Begin<day?day:e.Begin,end>day.AddDays(1)?day.AddDays(1):end);}
 }
}
public partial class Blocks {
 bool ChooseMembers(Window owner,List<ReportUser> members,HashSet<int> current,out HashSet<int> result,string title="表示・集計するメンバー") {
  var rows=members.Select(u=>new MemberChoice {Id=u.Id,Name=u.Name,Selected=current==null||current.Contains(u.Id)}).OrderBy(u=>u.Name).ToList();
  var w=new Window {Title=title,Owner=owner,Width=510,Height=620,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new DockPanel {Margin=new Thickness(16)};w.Content=root;
  var searchBox=new TextBox {Padding=new Thickness(6),Margin=new Thickness(4)};DockPanel.SetDock(searchBox,Dock.Top);root.Children.Add(searchBox);
  var buttons=new WrapPanel();DockPanel.SetDock(buttons,Dock.Bottom);root.Children.Add(buttons);
  var list=new ListBox {Margin=new Thickness(4)};root.Children.Add(list);var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(CheckBox.ContentProperty,new Binding("Name"));check.SetBinding(CheckBox.IsCheckedProperty,new Binding("Selected") {Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged});check.SetValue(CheckBox.MarginProperty,new Thickness(5));list.ItemTemplate=new DataTemplate {VisualTree=check};
  void Fill()=>list.ItemsSource=rows.Where(u=>Matches(u.Name,searchBox.Text)).ToList();searchBox.TextChanged+=(s,e)=>Fill();Fill();
  buttons.Children.Add(ButtonOf("全員",()=>{rows.ForEach(r=>r.Selected=true);Fill();}));buttons.Children.Add(ButtonOf("全解除",()=>{rows.ForEach(r=>r.Selected=false);Fill();}));buttons.Children.Add(ButtonOf("適用",()=>{w.DialogResult=true;}));buttons.Children.Add(ButtonOf("キャンセル",()=>w.Close()));
  bool accepted=w.ShowDialog()==true;result=accepted?rows.Where(r=>r.Selected).Select(r=>r.Id).ToHashSet():current;return accepted;
 }
 void ShowMemberCalendar(Window owner,ReportSnapshot snapshot,Action<Window> readyForTest=null) {
  var w=new Window {Title="メンバー実績カレンダー（閲覧専用）",Owner=owner,Width=1200,Height=800,MinWidth=650,MinHeight=450,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var root=new DockPanel {Margin=new Thickness(12),Background=Brushes.White};w.Content=root;var tools=new WrapPanel();DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);
  var mode=new ComboBox {ItemsSource=new[]{"1日・メンバー比較","1週間・ユーザー切替"},SelectedIndex=0,Width=195,Margin=new Thickness(4)};
  var date=new DatePicker {SelectedDate=snapshot.From??snapshot.Records.Select(r=>r.Begin.Date).DefaultIfEmpty(DateTime.Today).Min(),Width=150,Margin=new Thickness(4)};
  var users=new ComboBox {ItemsSource=snapshot.Users.Select(u=>new MemberChoice {Id=u.Id,Name=u.Name}).ToList(),SelectedIndex=snapshot.Users.Count>0?0:-1,Width=260,Margin=new Thickness(4)};
  var zoom=new ComboBox {ItemsSource=new[]{50,75,100,125,150,200,300},SelectedItem=100,Width=75,Margin=new Thickness(4)};
  tools.Children.Add(mode);tools.Children.Add(date);tools.Children.Add(users);tools.Children.Add(Label("倍率 %",12));tools.Children.Add(zoom);
  var note=Label("取得済み・選択したメンバーだけを表示。計測中は仮の5分枠。枠は開始～終了の経過時間、集計は休憩控除後の実績時間です。",11);note.TextWrapping=TextWrapping.Wrap;DockPanel.SetDock(note,Dock.Bottom);root.Children.Add(note);
  var headerCanvas=new Canvas {Height=34,Background=Brushes.White};var headerScroll=new ScrollViewer {Content=headerCanvas,Height=34,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};DockPanel.SetDock(headerScroll,Dock.Top);root.Children.Add(headerScroll);
  var canvas=new Canvas {Background=Brushes.White};var scroll=new ScrollViewer {Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);scroll.ScrollChanged+=(s,e)=>headerScroll.ScrollToHorizontalOffset(scroll.HorizontalOffset);double previousHour=120;
  void Draw() {
   double oldHourOffset=scroll.VerticalOffset/previousHour;canvas.Children.Clear();headerCanvas.Children.Clear();DateTime day=(date.SelectedDate??DateTime.Today).Date;bool weekly=mode.SelectedIndex==1;users.IsEnabled=weekly;
   double hour=120*(int)(zoom.SelectedItem??100)/100.0,colWidth=190,left=55,top=0;int count=weekly?(settings.ShowWeekends?7:5):snapshot.Users.Count;canvas.Width=left+Math.Max(1,count)*colWidth;canvas.Height=top+24*hour;headerCanvas.Width=canvas.Width;previousHour=hour;scroll.ScrollToVerticalOffset(oldHourOffset*hour);
   if(weekly)day=Monday(day);int chosen=(users.SelectedItem as MemberChoice)?.Id??-1;
   for(int h=0;h<24;h++){var label=Label(h.ToString("00")+":00",11);Canvas.SetTop(label,top+h*hour);canvas.Children.Add(label);var line=new Border {Width=canvas.Width-left,Height=1,Background=BrushOf("#DDE5EC"),IsHitTestVisible=false};Canvas.SetTop(line,top+h*hour);Canvas.SetLeft(line,left);canvas.Children.Add(line);}
   for(int column=0;column<count;column++) {
    DateTime d=weekly?day.AddDays(column):day;int user=weekly?chosen:snapshot.Users[column].Id;string caption=weekly?d.ToString("M/d (ddd)"):snapshot.Users[column].Name;
    var head=Label(caption,12);head.Width=colWidth-8;head.TextTrimming=TextTrimming.CharacterEllipsis;head.ToolTip=caption;Canvas.SetLeft(head,left+column*colWidth);headerCanvas.Children.Add(head);
    var line=new Border {Width=1,Height=canvas.Height,Background=BrushOf("#DDE5EC"),IsHitTestVisible=false};Canvas.SetLeft(line,left+column*colWidth);canvas.Children.Add(line);
    var rows=MemberReports.Slices(snapshot.Records.Where(r=>r.UserId==user),d).OrderBy(r=>r.Start).ThenBy(r=>r.End).ToList();
    // Assign lanes to each connected overlap group rather than hiding overlapping records.
    int start=0;while(start<rows.Count){int until=start+1;DateTime end=rows[start].End;while(until<rows.Count&&rows[until].Start<end){if(rows[until].End>end)end=rows[until].End;until++;}var lanes=new List<DateTime>();var assignments=new List<int>();for(int i=start;i<until;i++){int lane=lanes.FindIndex(t=>t<=rows[i].Start);if(lane<0){lane=lanes.Count;lanes.Add(DateTime.MinValue);}lanes[lane]=rows[i].End;assignments.Add(lane);}double width=(colWidth-4)/Math.Max(1,lanes.Count);
     for(int i=start;i<until;i++){var r=rows[i];var en=new Entry {Project=r.Record.Project,Activity=r.Record.Activity,Note=r.Record.Comment,Start=r.Start,Minutes=Math.Max(1,(int)(r.End-r.Start).TotalMinutes),RemoteId=r.Record.Id,ReadOnlyReason="他メンバーの実績（閲覧専用）"};var block=new Border {Width=Math.Max(8,width-2),Height=Math.Max(3,(r.End-r.Start).TotalHours*hour),Background=BrushOf(ColorFor(en.Project)),BorderBrush=Brushes.White,BorderThickness=new Thickness(1),ClipToBounds=true,ToolTip=EntryTip(en),Child=new TextBlock {Text=BlockCaption(en),TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=ReadableText(ColorFor(en.Project))}};Canvas.SetTop(block,top+r.Start.TimeOfDay.TotalHours*hour);Canvas.SetLeft(block,left+column*colWidth+assignments[i-start]*width+2);canvas.Children.Add(block);}
     start=until;
    }
   }
   if(snapshot.From.HasValue&&(day<snapshot.From.Value||day.AddDays(weekly?count-1:0)>snapshot.Through.Value))note.Text="取得した期間の範囲外を含みます。必要な期間を集計画面で再取得してください。";else note.Text="取得範囲: "+(snapshot.From.HasValue?snapshot.From.Value.ToString("d")+"～"+snapshot.Through.Value.ToString("d"):"全期間")+" / "+snapshot.Timezone+"。計測中は仮の5分枠。日またぎは日ごとに分割して表示。";
  }
  mode.SelectionChanged+=(s,e)=>Draw();date.SelectedDateChanged+=(s,e)=>Draw();users.SelectionChanged+=(s,e)=>Draw();zoom.SelectionChanged+=(s,e)=>Draw();
  scroll.PreviewMouseWheel+=(s,e)=>{if(!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))return;e.Handled=true;zoom.SelectedIndex=Math.Clamp(zoom.SelectedIndex+(e.Delta>0?1:-1),0,zoom.Items.Count-1);};
  Draw();w.Loaded+=(s,e)=>{scroll.ScrollToVerticalOffset(8*120);readyForTest?.Invoke(w);};w.ShowDialog();
 }
}

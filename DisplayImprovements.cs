using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public partial class Blocks {
 readonly DispatcherTimer zoomTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(120)};
 double zoomScale=1,zoomAnchor;bool zoomHooked;
 void PreviewZoom(int delta,double anchor) {
  if(Locked("ZoomPercent")||dragActive||!ApplyEditor())return;
  if(!zoomHooked){zoomHooked=true;zoomTimer.Tick+=(s,e)=>{if(dragActive||System.Windows.Input.Mouse.LeftButton==System.Windows.Input.MouseButtonState.Pressed)return;zoomTimer.Stop();SetZoom(Hour/144*100*zoomScale,zoomAnchor);};Closed+=(s,e)=>zoomTimer.Stop();}
  double minute=(calendarScroll.VerticalOffset+anchor)/(Hour*zoomScale);
  double percent=Math.Clamp(Hour/144*100*zoomScale*Math.Pow(1.1,delta/120.0),50,400);
  zoomScale=percent/(Hour/144*100);zoomAnchor=anchor;board.LayoutTransform=new ScaleTransform(1,zoomScale);calendarScroll.UpdateLayout();calendarScroll.ScrollToVerticalOffset(Math.Max(0,minute*Hour*zoomScale-anchor));
  zoomTimer.Stop();zoomTimer.Start();status.Text="表示倍率 "+Math.Round(percent)+"%";
 }
 internal static int TimeLabelStep(double hour)=>new[]{5,10,15,30,60}.First(step=>step*hour/60>=20);
 void DrawTimeLabels() {
  int step=TimeLabelStep(Hour);for(int minute=0;minute<1440;minute+=step){var label=Label($"{minute/60:00}:{minute%60:00}",minute%60==0?11:10);label.FontWeight=minute%60==0?FontWeights.SemiBold:FontWeights.Normal;label.Foreground=BrushOf(minute%60==0?"#30485E":"#718394");label.Margin=new Thickness(4,0,0,0);label.IsHitTestVisible=false;Canvas.SetTop(label,Math.Max(0,minute/60.0*Hour-6));board.Children.Add(label);}
 }
 void AddSelectedCommentTotals(StackPanel panel,List<Entry> selectedRows) {
  var entries=VisibleEntries();foreach(var group in selectedRows.GroupBy(e=>(e.Project,Comment:CommentStatistics.Key(e),Day:e.Start.Date)).Take(6)){
   var matching=entries.Where(e=>e.Project==group.Key.Project&&CommentStatistics.Key(e)==group.Key.Comment).ToList();string comment=group.Key.Comment.Length==0?"（コメントなし）":group.Key.Comment;
   var text=Label("コメント: "+comment+"\n"+group.Key.Day.ToString("M/d")+" 合計 "+Hours(matching.Where(e=>e.Start.Date==group.Key.Day).Sum(e=>e.Minutes))+" ／ 週合計 "+Hours(matching.Where(e=>e.Start>=Monday(group.Key.Day)&&e.Start<Monday(group.Key.Day).AddDays(7)).Sum(e=>e.Minutes)),12);text.TextTrimming=TextTrimming.CharacterEllipsis;text.ToolTip=group.Key.Project+"\n"+comment+"\n同じプロジェクト・同じコメントの実績を合計";text.Foreground=BrushOf("#1971C2");panel.Children.Add(text);
  }
 }
}


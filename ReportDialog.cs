using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

public partial class Blocks {
 void ShowServerReports() {
  if(communicating||service==null||needsRefresh){MessageBox.Show(this,"接続・再読込を完了してから集計してください。");return;}
  var api=service;
  var w=new Window {Title="サーバー実績集計（閲覧可能な全ユーザー）",Owner=this,Width=1250,Height=800,MinWidth=780,MinHeight=480,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var root=new DockPanel {Margin=new Thickness(16)};w.Content=root;
  var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
  var controls=new WrapPanel();top.Children.Add(controls);
  var from=new DatePicker {SelectedDate=week,Width=145,Margin=new Thickness(4)};var through=new DatePicker {SelectedDate=week.AddDays(6),Width=145,Margin=new Thickness(4)};
  var all=new CheckBox {Content="全期間",Margin=new Thickness(12,8,12,4)};
  controls.Children.Add(Label("開始日",12));controls.Children.Add(from);controls.Children.Add(Label("終了日（含む）",12));controls.Children.Add(through);controls.Children.Add(all);
  var fetch=new Button {Content="取得して集計",Padding=new Thickness(12,5,12,5),Margin=new Thickness(4)};controls.Children.Add(fetch);
  var members=new Button {Content="メンバーを選択…",IsEnabled=false,Margin=new Thickness(4),Padding=new Thickness(8)};controls.Children.Add(members);
  var blocks=new Button {Content="日／週のブロック表示…",IsEnabled=false,Margin=new Thickness(4),Padding=new Thickness(8)};controls.Children.Add(blocks);
  ReportSnapshot loadedSnapshot=null;HashSet<int> memberIds=null;
  var cancel=new Button {Content="取得をキャンセル",IsEnabled=false,Margin=new Thickness(4),Padding=new Thickness(12,5,12,5)};top.Children.Add(cancel);cancel.HorizontalAlignment=HorizontalAlignment.Left;
  var progress=new ProgressBar {Height=5,IsIndeterminate=true,Visibility=Visibility.Collapsed,Margin=new Thickness(4)};top.Children.Add(progress);
  var message=Label("期間を選び「取得して集計」を押してください。",12);message.TextWrapping=TextWrapping.Wrap;top.Children.Add(message);
  var note=Label("サーバー保存済みの実績のみ。期間・入力日数・月別は開始日時基準（接続ユーザーのタイムゾーン）。\n時間はKimaiのdurationを合計。計測中・時間不明は時間合計から除外します。入力日数は出勤日数や入力完了の判定ではありません。",11);note.TextWrapping=TextWrapping.Wrap;top.Children.Add(note);
  var searchRow=new DockPanel();top.Children.Add(searchRow);var searchLabel=Label("表示タブ内を検索",12);DockPanel.SetDock(searchLabel,Dock.Left);searchRow.Children.Add(searchLabel);var search=new TextBox {Margin=new Thickness(4),Padding=new Thickness(5)};searchRow.Children.Add(search);
  var close=ButtonOf("閉じる",()=>w.Close());DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
  var tabs=new TabControl {Margin=new Thickness(4,8,4,4)};root.Children.Add(tabs);
  CancellationTokenSource pending=null;bool loading=false,closeAfter=false;
  all.Checked+=(s,e)=>{from.IsEnabled=through.IsEnabled=false;};all.Unchecked+=(s,e)=>{from.IsEnabled=through.IsEnabled=true;};
  var delay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
  void Filter() {
   if(tabs.SelectedItem is not TabItem tab||tab.Tag is not DataTable table)return;
   table.DefaultView.RowFilter=ReportTables.FilterExpression(table,search.Text);
  }
  delay.Tick+=(s,e)=>{delay.Stop();Filter();};search.TextChanged+=(s,e)=>{delay.Stop();delay.Start();};tabs.SelectionChanged+=(s,e)=>{if(e.Source==tabs)Filter();};
  cancel.Click+=(s,e)=>pending?.Cancel();
  w.Closing+=(s,e)=>{if(loading){e.Cancel=true;closeAfter=true;pending?.Cancel();}};w.Closed+=(s,e)=>delay.Stop();
  async Task DisplayTables() {
   var snapshot=MemberReports.Filter(loadedSnapshot,memberIds);
    var tables=await Task.Run(()=>ReportTables.Build(snapshot,pending?.Token??CancellationToken.None));pending?.Token.ThrowIfCancellationRequested();
    tabs.Items.Clear();
    foreach(var table in tables) {
     var grid=new DataGrid {ItemsSource=table.DefaultView,IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,ClipboardCopyMode=DataGridClipboardCopyMode.IncludeHeader,SelectionMode=DataGridSelectionMode.Extended,FrozenColumnCount=1};
     grid.AutoGeneratingColumn+=(sender,args)=>{if(args.Column is DataGridTextColumn col&&col.Binding is Binding binding){if(args.PropertyType==typeof(DateTime))binding.StringFormat="yyyy/MM/dd HH:mm:ss";if(args.PropertyType==typeof(decimal))binding.StringFormat="0.####";col.MaxWidth=420;}};
     tabs.Items.Add(new TabItem {Header=table.TableName,Content=grid,Tag=table});
    }
    tabs.SelectedIndex=0;Filter();
    message.Text=(snapshot.From.HasValue?snapshot.From.Value.ToString("yyyy/MM/dd")+" ～ "+snapshot.Through.Value.ToString("yyyy/MM/dd"):"全期間")+" / "+snapshot.Timezone+" / "+snapshot.Records.Count+"件 / メンバー "+snapshot.Users.Count+"人 / 取得 "+snapshot.Retrieved.ToString("HH:mm:ss")+"\n"+snapshot.Notice;
  }
  members.Click+=async(s,e)=>{if(loadedSnapshot==null||loading)return;if(!ChooseMembers(w,loadedSnapshot.Users,memberIds,out var chosen))return;memberIds=chosen;controls.IsEnabled=false;loading=true;cancel.IsEnabled=true;progress.Visibility=Visibility.Visible;pending=new CancellationTokenSource();try{await DisplayTables();}catch(OperationCanceledException){tabs.Items.Clear();message.Text="集計をキャンセルしました。";}catch(Exception ex){tabs.Items.Clear();message.Text="集計できませんでした。再取得してください。";MessageBox.Show(w,SafeError(ex),"集計失敗");}finally{pending.Dispose();pending=null;loading=false;controls.IsEnabled=true;cancel.IsEnabled=false;progress.Visibility=Visibility.Collapsed;if(closeAfter)w.Close();}};
  blocks.Click+=(s,e)=>{if(loadedSnapshot!=null&&!loading)ShowMemberCalendar(w,MemberReports.Filter(loadedSnapshot,memberIds));};
  fetch.Click+=async(s,e)=>{
   DateTime? first=all.IsChecked==true?null:from.SelectedDate,last=all.IsChecked==true?null:through.SelectedDate;
   if(all.IsChecked!=true&&(!first.HasValue||!last.HasValue||first>last||last.Value.Date==DateTime.MaxValue.Date)){MessageBox.Show(w,"開始日と終了日を確認してください。");return;}
   loading=true;controls.IsEnabled=false;cancel.IsEnabled=true;progress.Visibility=Visibility.Visible;tabs.Items.Clear();loadedSnapshot=null;members.IsEnabled=blocks.IsEnabled=false;pending=new CancellationTokenSource();
   try {
    var snapshot=await api.ReadReportAsync(first,last,new Progress<string>(text=>message.Text=text),pending.Token);
    message.Text="テーブルを作成しています…";
    loadedSnapshot=snapshot;await DisplayTables();members.IsEnabled=blocks.IsEnabled=true;
   }catch(OperationCanceledException){message.Text="取得をキャンセルしました。部分的な結果は表示しません。";tabs.Items.Clear();}
   catch(Exception ex){message.Text="集計できませんでした。部分的な結果は表示しません。";tabs.Items.Clear();MessageBox.Show(w,SafeError(ex),"サーバー集計の取得失敗");}
   finally {pending.Dispose();pending=null;loading=false;controls.IsEnabled=true;cancel.IsEnabled=false;progress.Visibility=Visibility.Collapsed;if(closeAfter)w.Close();}
  };
  w.ShowDialog();
 }
}


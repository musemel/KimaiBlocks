using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

public sealed class BelowHours : IValueConverter {
 public decimal Minimum;
 public object Convert(object value,Type type,object parameter,CultureInfo culture)=>value is decimal hours&&hours<Minimum;
 public object ConvertBack(object value,Type type,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
public partial class Blocks {
 static List<int> ParseExcludedUserIds(string text){var ids=new List<int>();foreach(var part in (text??"").Normalize(NormalizationForm.FormKC).Split(new[]{',','\r','\n','\t',' ','、',';'},StringSplitOptions.RemoveEmptyEntries)){if(!int.TryParse(part,out int id)||id<=0)throw new ArgumentException("除外ユーザーには正の整数のユーザーIDを指定してください。カンマ・空白・改行で区切れます。");ids.Add(id);}return ids.Distinct().ToList();}
 static string ReportSettingsError(Exception ex)=>ex is ArgumentException?ex.Message:ex is UnauthorizedAccessException?"設定ファイルへの書き込み権限がありません。AppData\\Local\\KimaiBlocks のアクセス権を確認してください。":ex is IOException?"設定ファイルを保存できません。ファイルの使用状態や空き容量を確認してください。\n"+ex.Message:"設定の保存処理に失敗しました（"+ex.GetType().Name+"）。\n"+ex.Message;
 DataGrid ReportGrid(DataTable table,decimal? minimum=null) {
  var grid=new DataGrid {ItemsSource=table.DefaultView,IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,CanUserDeleteRows=false,EnableRowVirtualization=true,EnableColumnVirtualization=true,FrozenColumnCount=1,ClipboardCopyMode=DataGridClipboardCopyMode.IncludeHeader,SelectionMode=DataGridSelectionMode.Extended};
  ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Auto);
  grid.AutoGeneratingColumn+=(s,e)=>{e.Column.Header=table.Columns[e.PropertyName].Caption;if(e.Column is DataGridTextColumn column&&column.Binding is Binding binding){if(e.PropertyType==typeof(string))column.MinWidth=130;if(e.PropertyType==typeof(decimal)){binding.StringFormat="0.##";column.MinWidth=95;}if(e.PropertyType==typeof(DateTime))binding.StringFormat="yyyy/MM/dd HH:mm:ss";column.MaxWidth=420;}if(minimum.HasValue&&DateTime.TryParseExact(e.PropertyName,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)){e.Column.Header=DateTime.ParseExact(e.PropertyName,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd");e.Column.MinWidth=30;e.Column.MaxWidth=30;e.Column.Width=new DataGridLength(30);if(e.Column is DataGridTextColumn compact&&compact.Binding is Binding compactBinding){compactBinding.StringFormat="0.#";var textStyle=new Style(typeof(TextBlock));textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty,TextAlignment.Center));textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty,11.0));textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));compact.ElementStyle=textStyle;}var style=new Style(typeof(DataGridCell));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("["+e.PropertyName+"]"){StringFormat=e.PropertyName+" · {0:0.##} h"}));var trigger=new DataTrigger {Binding=new Binding("["+e.PropertyName+"]") {Converter=new BelowHours {Minimum=minimum.Value}},Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.MistyRose));trigger.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.DarkRed));style.Triggers.Add(trigger);e.Column.CellStyle=style;}};return grid;
 }
 void ExportReportCsv(Window owner,DataTable table) {if(table==null)return;var dialog=new SaveFileDialog {Title="表示中の集計をCSVへ出力",Filter="CSVファイル (*.csv)|*.csv",FileName=table.TableName+".csv",AddExtension=true};if(dialog.ShowDialog(owner)!=true)return;try{File.WriteAllText(dialog.FileName,AdvancedReportTables.Csv(table),new UTF8Encoding(true));}catch(Exception ex){MessageBox.Show(owner,SafeError(ex),"CSV出力失敗");}}
 bool ChooseReportProjects(Window owner,HashSet<int> current,out HashSet<int> result)=>ChooseMembers(owner,service.Projects.Where(p=>p.Visible!=false).Select(p=>new ReportUser {Id=p.Id.Value,Name=service.ProjectName(p.Id.Value)}).ToList(),current,out result,"明細に表示するプロジェクト");
 void ReportSettingsDialog(Window owner,System.Collections.Generic.List<ReportUser> users=null) {
  var w=new Window {Title="集計の設定",Owner=owner,Width=520,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(20)};w.Content=panel;
  panel.Children.Add(Label("集計対象外ユーザーID（カンマまたは改行区切り）",13));var ids=new TextBox {Text=string.Join(", ",settings.ExcludedReportUserIds),AcceptsReturn=true,Height=85,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(ids);LockSetting(ids,"ExcludedReportUserIds");
  if(users!=null){var choose=ButtonOf("一覧から除外ユーザーを選択…",()=>{try{if(ChooseMembers(w,users,ParseExcludedUserIds(ids.Text).ToHashSet(),out var chosen,"集計から除外するユーザー"))ids.Text=string.Join(", ",chosen.OrderBy(x=>x));}catch(Exception ex){MessageBox.Show(w,ReportSettingsError(ex),"入力確認");}});panel.Children.Add(choose);LockSetting(choose,"ExcludedReportUserIds");}
  panel.Children.Add(Label("日別表で赤く表示する基準（この時間未満、0〜24h）",13));var hours=new TextBox {Text=settings.ReportMinimumHours.ToString(CultureInfo.InvariantCulture),Margin=new Thickness(4),Padding=new Thickness(6)};panel.Children.Add(hours);LockSetting(hours,"ReportMinimumHours");
  panel.Children.Add(ButtonOf("保存",()=>{try{var excluded=ParseExcludedUserIds(ids.Text);if(excluded.Any(i=>i<=0)||!decimal.TryParse(hours.Text.Normalize(NormalizationForm.FormKC),NumberStyles.Number,CultureInfo.InvariantCulture,out var value)||value<0||value>24)throw new ArgumentException("ユーザーIDは正の整数、基準時間は0〜24で指定してください。");var old=settings;settings=System.Text.Json.JsonSerializer.Deserialize<ConnectionSettings>(System.Text.Json.JsonSerializer.Serialize(settings));settings.ExcludedReportUserIds=excluded;settings.ReportMinimumHours=value;try{StoreSettings(false);}catch{settings=old;throw;}w.DialogResult=true;}catch(Exception ex){MessageBox.Show(w,ReportSettingsError(ex),"集計設定を保存できません");}}));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MarkZither.KimaiDotNet.Models;

public sealed class CustomerOption {
 public int Id {get;set;}
 public string Name {get;set;}
 public override string ToString()=>Name+" [#"+Id+"]";
}
public partial class Blocks {
 async Task ProjectDialog() {
  if(communicating||service==null||needsRefresh){MessageBox.Show(this,"接続・再読込を完了してからプロジェクトを追加してください。");return;}
  List<CustomerOption> customers;
  await BeginProgress("追加先の顧客を取得しています…");
  try {customers=await service.ReadCustomersAsync();}
  catch(Exception ex){MessageBox.Show(progressWindow,SafeError(ex),"顧客を取得できません");return;}
  finally {EndProgress();}
  if(customers.Count==0){MessageBox.Show(this,"追加可能な顧客がありません。Kimaiで顧客を作成してください。");return;}
  var w=new Window {Title="プロジェクト追加（グローバルアクティビティ専用）",Owner=this,Width=590,Height=680,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  panel.Children.Add(Label("この操作はKimaiサーバーに即時反映されます。",15));
  var destination=Label(service.BaseUrl,12);destination.TextWrapping=TextWrapping.Wrap;panel.Children.Add(destination);
  var customer=new ComboBox {ItemsSource=customers,SelectedIndex=0,IsEditable=true,IsTextSearchEnabled=true,Padding=new Thickness(6)};
  TextSearch.SetTextPath(customer,"Name");
  var name=new TextBox {Padding=new Thickness(6),MaxLength=150};
  var comment=new TextBox {Height=60,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  var folder=new ComboBox {ItemsSource=new[]{"（フォルダ外）"}.Concat(state.Folders).ToArray(),SelectedIndex=0,Padding=new Thickness(6)};
  var billable=new CheckBox {Content="請求対象のプロジェクト",IsChecked=true,Margin=new Thickness(4,12,4,8)};
  panel.Children.Add(Label("追加先の顧客（入力で検索して選択）",12));panel.Children.Add(customer);
  panel.Children.Add(Label("プロジェクト名",12));panel.Children.Add(name);
  panel.Children.Add(Label("説明",12));panel.Children.Add(comment);
  panel.Children.Add(Label("このクライアントの表示フォルダ",12));panel.Children.Add(folder);panel.Children.Add(billable);
  var info=Label("グローバルアクティビティを有効にして作成します。\nプロジェクト専用アクティビティは作成しません。\nフォルダ分類はこのクライアントだけに保存されます。",12);info.TextWrapping=TextWrapping.Wrap;panel.Children.Add(info);
  var consent=new CheckBox {Content="顧客・プロジェクト名・追加先フォルダを確認しました",Margin=new Thickness(4,12,4,8)};panel.Children.Add(consent);
  bool sending=false,submitted=false;w.Closing+=(s,e)=>{if(sending)e.Cancel=true;};
  Button create=null;create=ButtonOf("内容を確認して作成…",async()=>{
   if(submitted)return;
   var chosen=customer.SelectedItem as CustomerOption;string projectName=name.Text.Trim();
   if(chosen==null||projectName.Length==0||consent.IsChecked!=true){MessageBox.Show(w,"顧客を一覧から選択し、プロジェクト名を入力して、確認チェックを付けてください。","入力確認");return;}
   string folderName=folder.SelectedIndex<=0?null:(string)folder.SelectedItem;
   string summary="接続先: "+service.BaseUrl+"\n顧客: "+chosen+"\nプロジェクト: "+projectName+"\nフォルダ: "+(folderName??"フォルダ外")+"\nグローバルアクティビティ: 有効\n請求対象: "+(billable.IsChecked==true?"はい":"いいえ")+"\n説明: "+comment.Text;
   if(MessageBox.Show(w,summary+"\n\nこの内容で作成へ進みますか？","作成内容の確認（1/2）",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
   if(MessageBox.Show(w,"最終確認（2/2）\n\n「"+chosen.Name+"」に「"+projectName+"」を作成します。\n定期保存を待たずにサーバーへ送信します。\n実行してよろしいですか？","プロジェクトを作成しますか",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
   sending=true;panel.IsEnabled=false;SetCommunicating(true);ProjectEntity created=null;
   try {
    await service.ReloadCatalogAsync();
    if(service.Projects.Any(p=>p.Customer==chosen.Id&&string.Equals(p.Name,projectName,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("同じ顧客に同名のプロジェクトがあります。名前を変更してください。");
    submitted=true;
    created=await service.CreateGlobalProjectAsync(chosen.Id,projectName,comment.Text,billable.IsChecked==true);
    string label=projectName+" [#"+created.Id.Value+"]";
    if(folderName!=null)state.ProjectFolders[label]=folderName;
    state.Hidden.Remove(label);Persist();
    await LoadCatalog(service,true);await RefreshView();Persist();
    MessageBox.Show(w,"プロジェクトを作成しました。\n"+projectName+" [#"+created.Id+"]\nグローバルアクティビティ: 有効","作成完了");
    sending=false;w.Close();
   }catch(Exception ex){
    string detail=created!=null?"作成は完了しました（#"+created.Id+"）。一覧更新またはフォルダ保存に失敗しました。再作成せず、再読込してください。":submitted?"作成の結果を確認できません。重複を避けるため、この画面からの再送信を止めました。閉じて再読込し、Kimai上の登録状況を確認してください。":"入力確認に失敗しました。";
    MessageBox.Show(w,SafeError(ex)+"\n\n"+detail,"プロジェクト追加の確認");
   }finally {sending=false;SetCommunicating(false);panel.IsEnabled=true;create.IsEnabled=!submitted;}
  });panel.Children.Add(create);panel.Children.Add(ButtonOf("閉じる",()=>w.Close()));
  name.TextChanged+=(s,e)=>consent.IsChecked=false;customer.SelectionChanged+=(s,e)=>consent.IsChecked=false;folder.SelectionChanged+=(s,e)=>consent.IsChecked=false;
  w.ShowDialog();
 }
}

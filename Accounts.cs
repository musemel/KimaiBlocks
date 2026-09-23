using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

public sealed class AccountProfile {
 public string Id {get;set;}=Guid.NewGuid().ToString("N");
 public string Name {get;set;}="";
 public string Url {get;set;}="";
 public string Username {get;set;}="";
 public string ProtectedToken {get;set;}="";
 public bool Legacy {get;set;}
 public bool AllowHttp {get;set;}
 public int UserId {get;set;}
 [JsonIgnore] public string TokenDisplay=>string.IsNullOrEmpty(ProtectedToken)?"未設定":"••••••••";
 [JsonIgnore] public string Authentication=>Legacy?"旧方式":"Bearer";
 public AccountProfile Copy()=>(AccountProfile)MemberwiseClone();
 public static void Select(ConnectionSettings settings,AccountProfile account) {
  settings.ActiveAccountId=account.Id;settings.Url=account.Url;settings.Username=account.Username;settings.ProtectedToken=account.ProtectedToken;settings.AllowHttp=account.AllowHttp;settings.Legacy=account.Legacy;settings.UserId=account.UserId;
 }
 public static void Migrate(ConnectionSettings settings) {
  settings.Accounts??=new List<AccountProfile>();
  if(settings.Accounts.Count==0&&!string.IsNullOrWhiteSpace(settings.Url))settings.Accounts.Add(new AccountProfile {Name=string.IsNullOrWhiteSpace(settings.Username)?"既存のアカウント":settings.Username,Url=settings.Url,Username=settings.Username,ProtectedToken=settings.ProtectedToken,Legacy=settings.Legacy,AllowHttp=settings.AllowHttp,UserId=settings.UserId});
  var active=settings.Accounts.FirstOrDefault(a=>a.Id==settings.ActiveAccountId)??settings.Accounts.FirstOrDefault();if(active!=null)Select(settings,active);
 }
 public static void Tests() {
  var settings=new ConnectionSettings {Url="https://example.test",Username="first",ProtectedToken="encrypted-placeholder",UserId=7};Migrate(settings);
  if(settings.Accounts.Count!=1||settings.Accounts[0].ProtectedToken!="encrypted-placeholder"||settings.UserId!=7)throw new Exception("Account migration failed");
  Migrate(settings);if(settings.Accounts.Count!=1)throw new Exception("Duplicate migration");
  var other=new AccountProfile {Url="https://other.example.test",Username="second",ProtectedToken="other-encrypted",UserId=9};settings.Accounts.Add(other);Select(settings,other);
  settings=JsonSerializer.Deserialize<ConnectionSettings>(JsonSerializer.Serialize(settings));Migrate(settings);
  if(settings.Username!="second"||settings.UserId!=9||settings.ProtectedToken!="other-encrypted"||settings.Accounts[0].UserId!=7)throw new Exception("Account selection persistence failed");
 }
}
public partial class Blocks {
 void ConnectionDialog() {
  if(communicating)return;
  AccountProfile.Migrate(settings);
  var accounts=new ObservableCollection<AccountProfile>(settings.Accounts.Select(a=>a.Copy()));
  var w=new Window {Title="設定 / アカウント",Owner=this,Width=880,Height=750,MinWidth=740,MinHeight=620,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new DockPanel {Margin=new Thickness(20)};w.Content=panel;
  var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);
  var seconds=new TextBox {Text=settings.SaveSeconds.ToString(),Width=100};var minutes=new TextBox {Text=settings.CatalogMinutes.ToString(),Width=100};
  var options=new WrapPanel();options.Children.Add(Label("保存間隔（秒、10〜3600）",12));options.Children.Add(seconds);options.Children.Add(Label("一覧キャッシュ（分、1〜1440）",12));options.Children.Add(minutes);bottom.Children.Add(options);
  var weekends=new CheckBox {Content="カレンダーに土日を表示",IsChecked=settings.ShowWeekends,Margin=new Thickness(4,12,4,8)};bottom.Children.Add(weekends);
  bottom.Children.Add(ButtonOf("休日・休み時間の設定…",CalendarDialog));
  var note=Label("一覧からアカウントを選んで「選択したアカウントで保存して接続」を押してください。\nBearer認証ではAPIトークンが接続ユーザーを決定します。ユーザー名は管理用の表示です。\nトークンはWindowsユーザー用に暗号化して保存します。",12);note.TextWrapping=TextWrapping.Wrap;bottom.Children.Add(note);
  var heading=Label("接続アカウント",17);DockPanel.SetDock(heading,Dock.Top);panel.Children.Add(heading);
  var actions=new StackPanel {Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Top);panel.Children.Add(actions);
  var table=new DataGrid {ItemsSource=accounts,AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,SelectionUnit=DataGridSelectionUnit.FullRow,Margin=new Thickness(0,8,0,16)};
  void Column(string title,string property,double width){table.Columns.Add(new DataGridTextColumn {Header=title,Binding=new Binding(property),Width=new DataGridLength(width,DataGridLengthUnitType.Star)});}
  Column("表示名","Name",1);Column("ユーザー名","Username",1);Column("Kimai URL","Url",1.6);Column("APIトークン","TokenDisplay",.7);Column("認証","Authentication",.6);
  table.SelectedItem=accounts.FirstOrDefault(a=>a.Id==settings.ActiveAccountId)??accounts.FirstOrDefault();panel.Children.Add(table);
  void EditSelected(){var selected=table.SelectedItem as AccountProfile;if(selected==null)return;var updated=EditAccount(w,selected);if(updated!=null){int index=accounts.IndexOf(selected);accounts[index]=updated;table.SelectedItem=updated;}}
  actions.Children.Add(ButtonOf("追加…",()=>{var account=EditAccount(w,new AccountProfile {Url=settings.Url,AllowHttp=settings.AllowHttp});if(account!=null){accounts.Add(account);table.SelectedItem=account;}}));
  actions.Children.Add(ButtonOf("編集…",EditSelected));
  actions.Children.Add(ButtonOf("一覧から削除",()=>{var account=table.SelectedItem as AccountProfile;if(account==null)return;if(MessageBox.Show(w,"「"+account.Name+"」を保存するアカウント一覧から削除しますか？\nKimai側のユーザー・実績・APIトークン自体は削除しません。\n一覧の変更は「保存して接続」で確定します。","アカウント一覧",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)!=MessageBoxResult.Yes)return;accounts.Remove(account);table.SelectedItem=accounts.FirstOrDefault();}));
  table.MouseDoubleClick+=(s,e)=>{if(ItemsControl.ContainerFromElement(table,e.OriginalSource as DependencyObject) is DataGridRow)EditSelected();};
  bottom.Children.Add(ButtonOf("選択したアカウントで保存して接続",async()=>{
   var account=table.SelectedItem as AccountProfile;
   if(account==null){MessageBox.Show(w,"接続するアカウントを追加・選択してください。");return;}
   if(!int.TryParse(seconds.Text,out int sec)||sec<10||sec>3600||!int.TryParse(minutes.Text,out int min)||min<1||min>1440){MessageBox.Show(w,"保存間隔とキャッシュ期間を範囲内で指定してください。");return;}
   if(state.Pending.Count>0){MessageBox.Show(w,"未保存の実績があります。設定画面を閉じ、「今すぐ保存」を完了してから切り替えてください。","アカウント切替を中止");return;}
   var previous=settings;
   try {
    // Persist current-account UI settings before releasing the old account.
    Persist();
    var updated=JsonSerializer.Deserialize<ConnectionSettings>(JsonSerializer.Serialize(settings));
    updated.Accounts=accounts.Select(a=>a.Copy()).ToList();updated.SaveSeconds=sec;updated.CatalogMinutes=min;updated.ShowWeekends=weekends.IsChecked==true;AccountProfile.Select(updated,updated.Accounts.Single(a=>a.Id==account.Id));
    settings=updated;try {StoreSettings();}catch {settings=previous;throw;}
    service?.Dispose();service=null;needsRefresh=true;savePaused=true;saveTimer.Stop();file=null;state=new State();Projects=Array.Empty<string>();selected=null;connectionBadge.Text="接続待ち: "+account.Name;PopulateProjectList();Populate();Render();
    w.Close();await ConnectAsync();
   }catch(Exception ex){if(w.IsVisible)MessageBox.Show(w,SafeError(ex),"設定保存失敗");else MessageBox.Show(this,SafeError(ex),"接続失敗");}
  }));
  bottom.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();
 }
 AccountProfile EditAccount(Window owner,AccountProfile source) {
  var w=new Window {Title="アカウント設定",Owner=owner,Width=560,Height=590,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Thickness(22)};w.Content=panel;
  var name=new TextBox {Text=source.Name};var url=new TextBox {Text=source.Url};var username=new TextBox {Text=source.Username};var token=new PasswordBox();
  bool tokenReadable=true;string oldToken="";
  try {if(!string.IsNullOrEmpty(source.ProtectedToken))oldToken=Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(source.ProtectedToken),null,DataProtectionScope.CurrentUser));token.Password=oldToken;}catch {tokenReadable=false;}
  string[] labels={"表示名（例: 業務用・個人用）","Kimai URL","ユーザー名（Bearerでは管理用、旧方式では認証に使用）","APIトークン"};Control[] controls={name,url,username,token};
  for(int i=0;i<labels.Length;i++){panel.Children.Add(Label(labels[i],12));controls[i].Padding=new Thickness(6);panel.Children.Add(controls[i]);}
  var legacy=new CheckBox {Content="旧認証方式（ユーザー名＋APIトークン）",IsChecked=source.Legacy,Margin=new Thickness(4,14,4,8)};
  var http=new CheckBox {Content="この接続先でHTTPを許可（通信は暗号化されません）",IsChecked=source.AllowHttp,Margin=new Thickness(4,8,4,8)};url.TextChanged+=(s,e)=>http.IsChecked=false;panel.Children.Add(legacy);panel.Children.Add(http);
  if(!tokenReadable)panel.Children.Add(Label("保存済みトークンを復号できません。再入力してください。",12));
  AccountProfile result=null;
  panel.Children.Add(ButtonOf("一覧に反映",()=>{
   try {
    string normalized=KimaiService.NormalizeUrl(url.Text,http.IsChecked==true);
    if(string.IsNullOrWhiteSpace(name.Text)||string.IsNullOrWhiteSpace(token.Password))throw new ArgumentException("表示名とAPIトークンを入力してください。");
    if(legacy.IsChecked==true&&string.IsNullOrWhiteSpace(username.Text))throw new ArgumentException("旧認証方式ではユーザー名が必要です。");
    bool unchanged=tokenReadable&&normalized==source.Url&&username.Text==source.Username&&legacy.IsChecked==source.Legacy&&token.Password==oldToken;
    result=new AccountProfile {Id=source.Id,Name=name.Text.Trim(),Url=normalized,Username=username.Text.Trim(),Legacy=legacy.IsChecked==true,AllowHttp=http.IsChecked==true,UserId=unchanged?source.UserId:0,ProtectedToken=unchanged?source.ProtectedToken:Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token.Password),null,DataProtectionScope.CurrentUser))};w.Close();
   }catch(Exception ex){MessageBox.Show(w,SafeError(ex),"入力確認");}
  }));panel.Children.Add(ButtonOf("キャンセル",()=>w.Close()));w.ShowDialog();return result;
 }
}

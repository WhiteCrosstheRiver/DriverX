namespace DriverX.Desktop;
internal static class UiText
{
    public static string Language="zh-CN";
    static readonly Dictionary<string,string[]> Texts=new()
    {
        ["打开"]=["Open","開く","Ouvrir"], ["挂载"]=["Mount","マウント","Monter"], ["卸载"]=["Unmount","解除","Démonter"],
        ["编辑"]=["Edit","編集","Modifier"], ["删除"]=["Delete","削除","Supprimer"],
        ["已挂载"]=["Mounted","接続済み","Monté"], ["未挂载"]=["Not mounted","未接続","Non monté"], ["连接异常"]=["Connection error","接続エラー","Erreur de connexion"],
        ["磁盘"]=["Drive","ドライブ","Disque"], ["名称"]=["Name","名前","Nom"], ["状态"]=["Status","状態","État"], ["服务器"]=["Server","サーバー","Serveur"], ["协议"]=["Protocols","プロトコル","Protocoles"], ["操作"]=["Actions","操作","Actions"],
        ["卡片"]=["Cards","カード","Cartes"], ["列表"]=["List","一覧","Liste"], ["巨大"]=["Huge","特大","Très grand"], ["大"]=["Large","大","Grand"], ["中"]=["Medium","中","Moyen"], ["小"]=["Small","小","Petit"], ["迷你"]=["Mini","最小","Mini"],
        ["我的磁盘"]=["My drives","マイドライブ","Mes disques"], ["活动"]=["Activity","アクティビティ","Activité"], ["设置"]=["Settings","設定","Paramètres"], ["完全退出"]=["Quit completely","完全に終了","Quitter complètement"], ["关闭到托盘"]=["Hide to tray","トレイに格納","Réduire dans la zone de notification"],
        ["已挂载磁盘优先显示"]=["Show mounted drives first","接続済みを先に表示","Afficher les disques montés en premier"],
        ["取消"]=["Cancel","キャンセル","Annuler"], ["保存连接"]=["Save connection","接続を保存","Enregistrer"], ["添加远程存储"]=["Add remote storage","ストレージを追加","Ajouter un stockage"],
        ["名称 *"]=["Name *","名前 *","Nom *"], ["协议 *"]=["Protocol *","プロトコル *","Protocole *"], ["主机 *"]=["Host *","ホスト *","Hôte *"], ["端口"]=["Port","ポート","Port"], ["用户名"]=["Username","ユーザー名","Utilisateur"], ["密码（可选）"]=["Password (optional)","パスワード（任意）","Mot de passe (facultatif)"], ["远程目录"]=["Remote path","リモートパス","Chemin distant"], ["盘符 / 图标"]=["Drive / icon","ドライブ / アイコン","Disque / icône"], ["SSH 私钥（可选）"]=["SSH key (optional)","SSH鍵（任意）","Clé SSH (facultative)"]
    };
    static UiText() {
        using var stream=typeof(UiText).Assembly.GetManifestResourceStream("DriverX.translations");
        if(stream is null)return; using var reader=new System.IO.StreamReader(stream);
        while(reader.ReadLine() is {} line){var parts=line.Split('|');if(parts.Length==4)Texts[parts[0]]=parts[1..];}
    }
    public static string T(string text){
        var key=Texts.ContainsKey(text)?text:Texts.FirstOrDefault(p=>p.Value.Contains(text)).Key;
        if(key is null)return text;
        return Language=="zh-CN"?key:Texts[key][Language=="ja-JP"?1:Language=="fr-FR"?2:0];
    }
}

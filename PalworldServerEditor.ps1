# Palworld Server Editor - Windows PowerShell 5.1+ / WinForms
# No external modules. Run using Start Editor.cmd.
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$groups = [ordered]@{
  "Mondo" = @(
    @("ExpRate","Esperienza (x)","number",0.1,20,0.1),
    @("PalCaptureRate","Probabilita cattura (x)","number",0.1,10,0.05),
    @("PalSpawnNumRate","Numero Pal selvatici (x)","number",0.1,5,0.1),
    @("DayTimeSpeedRate","Velocita giorno (x)","number",0.1,10,0.1),
    @("NightTimeSpeedRate","Velocita notte (x)","number",0.1,10,0.1),
    @("SupplyDropSpan","Intervallo meteore/rifornimenti (min)","number",1,1440,5),
    @("PalEggDefaultHatchingTime","Tempo schiusa uova (ore)","number",0,240,0.5)
  )
  "Basi e risorse" = @(
    @("BaseCampWorkerMaxNum","Pal al lavoro per base","integer",1,50,1),
    @("BaseCampMaxNumInGuild","Numero basi per gilda","integer",1,20,1),
    @("GuildPlayerMaxNum","Giocatori per gilda","integer",1,100,1),
    @("CollectionDropRate","Quantita raccolta (x)","number",0.1,20,0.1),
    @("EnemyDropItemRate","Drop nemici (x)","number",0.1,20,0.1),
    @("ItemWeightRate","Peso oggetti (x)","number",0.01,10,0.05),
    @("WorkSpeedRate","Velocita lavoro (x)","number",0.1,20,0.1),
    @("BuildObjectDeteriorationDamageRate","Deterioramento strutture (0 = off)","number",0,10,0.1),
    @("bAllowGlobalPalboxExport","Esportazione Global Palbox","bool"),
    @("bAllowGlobalPalboxImport","Importazione Global Palbox","bool")
  )
  "Combattimento" = @(
    @("PalDamageRateAttack","Danno inflitto dai Pal (x)","number",0.1,10,0.1),
    @("PlayerDamageRateAttack","Danno inflitto dai giocatori (x)","number",0.1,10,0.1),
    @("PlayerStaminaDecreaceRate","Consumo stamina (x)","number",0.1,10,0.1),
    @("DeathPenalty","Penalita alla morte","choice",@("None","Item","ItemAndEquipment","All")),
    @("bEnableInvaderEnemy","Raid alle basi","bool"),
    @("bIsPvP","PvP","bool"),
    @("bEnableFriendlyFire","Fuoco amico","bool")
  )
  "Server" = @(
    @("ServerName","Nome server","string"),
    @("ServerDescription","Descrizione","string"),
    @("ServerPlayerMaxNum","Numero massimo giocatori","integer",1,128,1),
    @("PublicPort","Porta pubblica","integer",1,65535,1),
    @("AutoSaveSpan","Autosave (minuti)","number",1,240,1),
    @("bIsUseBackupSaveData","Backup integrati","bool"),
    @("bShowPlayerList","Mostra elenco giocatori","bool"),
    @("bAllowClientMod","Consenti mod lato client","bool"),
    @("RESTAPIEnabled","REST API abilitata (solo rete fidata)","bool"),
    @("RESTAPIPort","Porta REST API","integer",1,65535,1),
    @("bEnableVoiceChat","Chat vocale","bool")
  )
}
$controlsByKey = @{}
$originalValues = @{}
$raw = ""
$encoding = New-Object System.Text.UTF8Encoding($false)
$path = $null

$form = New-Object Windows.Forms.Form
$form.Text = "Palworld Server Editor"
$form.Size = New-Object Drawing.Size(850,720)
$form.MinimumSize = New-Object Drawing.Size(750,580)
$form.StartPosition = "CenterScreen"
$form.BackColor = [Drawing.Color]::FromArgb(245,247,250)
$form.Font = New-Object Drawing.Font("Segoe UI",10)
$top = New-Object Windows.Forms.Panel
$top.Dock = "Top"; $top.Height = 100
$form.Controls.Add($top)
$title = New-Object Windows.Forms.Label
$title.Text = "PALWORLD  |  SERVER EDITOR"; $title.Font = New-Object Drawing.Font("Segoe UI",17,[Drawing.FontStyle]::Bold)
$title.Location = New-Object Drawing.Point(18,12); $title.AutoSize=$true
$top.Controls.Add($title)
$pathBox = New-Object Windows.Forms.TextBox
$pathBox.Location = New-Object Drawing.Point(18,57); $pathBox.Size=New-Object Drawing.Size(610,29)
$pathBox.Anchor = "Top,Left,Right"; $pathBox.ReadOnly=$true
$top.Controls.Add($pathBox)
$open = New-Object Windows.Forms.Button
$open.Text="Apri INI"; $open.Size=New-Object Drawing.Size(105,31)
$open.Location=New-Object Drawing.Point(645,55); $open.Anchor="Top,Right"
$top.Controls.Add($open)

$bottom = New-Object Windows.Forms.Panel
$bottom.Dock="Bottom"; $bottom.Height=80
$form.Controls.Add($bottom)
$status=New-Object Windows.Forms.Label
$status.AutoSize=$false; $status.Text="Apri il file PalWorldSettings.ini effettivo del server."
$status.Location=New-Object Drawing.Point(18,7); $status.Size=New-Object Drawing.Size(760,29)
$status.Anchor="Top,Left,Right"; $bottom.Controls.Add($status)
$save=New-Object Windows.Forms.Button
$save.Text="Salva modifiche e backup"; $save.Size=New-Object Drawing.Size(230,33)
$save.Location=New-Object Drawing.Point(18,39); $save.Enabled=$false
$bottom.Controls.Add($save)
$reload=New-Object Windows.Forms.Button
$reload.Text="Ricarica"; $reload.Size=New-Object Drawing.Size(100,33)
$reload.Location=New-Object Drawing.Point(265,39); $reload.Enabled=$false
$bottom.Controls.Add($reload)
$hint=New-Object Windows.Forms.Label
$hint.Text="Arresta il server prima di salvare."; $hint.AutoSize=$true
$hint.Location=New-Object Drawing.Point(390,46); $bottom.Controls.Add($hint)

$tabs=New-Object Windows.Forms.TabControl
$tabs.Dock="Fill"; $tabs.Padding=New-Object Drawing.Point(15,8)
$form.Controls.Add($tabs)
$tabs.BringToFront()

foreach($category in $groups.Keys) {
  $tab=New-Object Windows.Forms.TabPage
  $tab.Text=$category; $tab.BackColor=[Drawing.Color]::White; $tab.AutoScroll=$true
  [void]$tabs.TabPages.Add($tab)
  $y=16
  foreach($field in $groups[$category]) {
    $key=$field[0]; $label=$field[1]; $kind=$field[2]
    $lbl=New-Object Windows.Forms.Label
    $lbl.Text=$label; $lbl.Location=New-Object Drawing.Point(18,$y+7)
    $lbl.Size=New-Object Drawing.Size(435,29); $lbl.Anchor="Top,Left"
    $tab.Controls.Add($lbl)
    if($kind -eq "number" -or $kind -eq "integer") {
      $ctrl=New-Object Windows.Forms.NumericUpDown
      $ctrl.DecimalPlaces= if($kind -eq "integer"){0}else{2}
      $ctrl.Minimum=[decimal]$field[3]; $ctrl.Maximum=[decimal]$field[4]
      $ctrl.Increment=[decimal]$field[5]
      $ctrl.ThousandsSeparator=$false
    } elseif($kind -eq "bool" -or $kind -eq "choice") {
      $ctrl=New-Object Windows.Forms.ComboBox
      $ctrl.DropDownStyle="DropDownList"
      if($kind -eq "bool") { [void]$ctrl.Items.AddRange([object[]]@("True","False")) }
      else { [void]$ctrl.Items.AddRange([object[]]$field[3]) }
    } else {
      $ctrl=New-Object Windows.Forms.TextBox
    }
    $ctrl.Location=New-Object Drawing.Point(465,$y)
    $ctrl.Size=New-Object Drawing.Size(280,30)
    $ctrl.Anchor="Top,Left,Right"; $ctrl.Enabled=$false
    $tab.Controls.Add($ctrl)
    $controlsByKey[$key]=@($ctrl,$kind,$field)
    $y+=43
  }
}

function Unquote([string]$value) {
  if($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) {
    return $value.Substring(1,$value.Length-2).Replace('\"','"').Replace('""','"')
  }
  return $value
}
function Read-Settings([string]$file) {
  $script:raw=[System.IO.File]::ReadAllText($file)
  if($raw -notmatch '(?s)OptionSettings=\(') { throw "OptionSettings non trovato nel file." }
  $script:originalValues=@{}
  foreach($key in $controlsByKey.Keys) {
    $match=[regex]::Match($raw, '(?<=\(|,)' + [regex]::Escape($key) + '=(?:"(?:\\.|[^"])*"|[^,)]*)')
    if(!$match.Success) { $controlsByKey[$key][0].Enabled=$false; continue }
    $value=$match.Value.Substring($key.Length+1)
    $script:originalValues[$key]=$value
    $c=$controlsByKey[$key][0]; $kind=$controlsByKey[$key][1]
    if($kind -eq "number" -or $kind -eq "integer") {
      $num=0.0
      if(![double]::TryParse($value,[System.Globalization.NumberStyles]::Float,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$num)) { $c.Enabled=$false; continue }
      if([decimal]$num -lt $c.Minimum -or [decimal]$num -gt $c.Maximum) {
        $c.Enabled=$false; continue
      }
      $c.Value=[decimal]$num
    } elseif($kind -eq "string") {
      $c.Text=Unquote $value
    } else {
      $idx=$c.Items.IndexOf($value)
      if($idx -lt 0) { $c.Enabled=$false; continue }
      $c.SelectedIndex=$idx
    }
    $c.Enabled=$true
  }
  $script:path=$file
  $pathBox.Text=$file
  $status.Text="File caricato. I parametri non presenti o fuori intervallo restano invariati."
  $save.Enabled=$true; $reload.Enabled=$true
  if($raw -match 'Changes to this file will NOT be reflected on the server') {
    [Windows.Forms.MessageBox]::Show("ATTENZIONE: il file contiene l'avviso del modello di esempio. Verifica che sia Pal\Saved\Config\WindowsServer\PalWorldSettings.ini.","Possibile file di esempio","OK","Warning") | Out-Null
  }
}
function Pick-File {
  $dlg=New-Object Windows.Forms.OpenFileDialog
  $dlg.Title="Seleziona il file reale PalWorldSettings.ini"
  $dlg.Filter="INI Palworld (*.ini)|*.ini|Tutti i file (*.*)|*.*"
  $dlg.FileName="PalWorldSettings.ini"
  if($dlg.ShowDialog() -eq "OK") {
    try { Read-Settings $dlg.FileName } catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message,"Errore apertura","OK","Error") | Out-Null }
  }
}
$open.Add_Click({ Pick-File })
$reload.Add_Click({ if($path) { try { Read-Settings $path }catch { [Windows.Forms.MessageBox]::Show($_.Exception.Message) | Out-Null } } })
$save.Add_Click({
  if(!$path) { return }
  try {
    $current=[System.IO.File]::ReadAllText($path)
    if($current -cne $raw) {
      [Windows.Forms.MessageBox]::Show("Il file e stato modificato esternamente dopo l'apertura. Ricaricalo prima di salvare.","Conflitto","OK","Warning") | Out-Null
      return
    }
    $updated=$raw
    $changes=0
    foreach($key in $originalValues.Keys) {
      $ctrl=$controlsByKey[$key][0]; $kind=$controlsByKey[$key][1]
      if(!$ctrl.Enabled) {continue}
      if($kind -eq "number" -or $kind -eq "integer") {
        $value=$ctrl.Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
      } elseif($kind -eq "string") {
        $value='"' + $ctrl.Text.Replace('"','\"') + '"'
      } else { $value=[string]$ctrl.SelectedItem }
      $old=$originalValues[$key]
      if($kind -eq "number" -or $kind -eq "integer") {
        $oldnum=0.0
        if([double]::TryParse($old,[System.Globalization.NumberStyles]::Float,[System.Globalization.CultureInfo]::InvariantCulture,[ref]$oldnum)) {
          if([decimal]$oldnum -eq $ctrl.Value) {continue}
        }
      } elseif($kind -eq "string") {
        if((Unquote $old) -ceq $ctrl.Text) {continue}
      } elseif($old -ceq $value) { continue }
      $pattern='(?<=\(|,)' + [regex]::Escape($key) + '=(?:"(?:\\.|[^"])*"|[^,)]*)'
      $updated=[regex]::Replace($updated,$pattern, [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        return $key+'='+$value
      },[System.Text.RegularExpressions.RegexOptions]::None)
      $changes++
    }
    if($changes -eq 0) { $status.Text="Nessuna modifica da salvare."; return }
    $backup=$path+'.'+(Get-Date -Format "yyyyMMdd-HHmmss")+'.bak'
    [System.IO.File]::Copy($path,$backup)
    $temp=$path+'.tmp'
    try {
      [System.IO.File]::WriteAllText($temp,$updated,$encoding)
      [System.IO.File]::Copy($temp,$path,$true)
    } finally {
      if([System.IO.File]::Exists($temp)){ [System.IO.File]::Delete($temp) }
    }
    Read-Settings $path
    $status.Text="Salvati $changes parametri. Backup: $(Split-Path $backup -Leaf)"
    [Windows.Forms.MessageBox]::Show("Modifiche salvate ($changes). Backup creato nella stessa cartella. Riavvia il server per applicarle.","Salvataggio completato","OK","Information") | Out-Null
  } catch {
    [Windows.Forms.MessageBox]::Show($_.Exception.Message,"Errore salvataggio","OK","Error") | Out-Null
  }
})
$desktopIni=Join-Path $PSScriptRoot "PalWorldSettings.ini"
if([IO.File]::Exists($desktopIni)) { try { Read-Settings $desktopIni }catch{} }
[void][Windows.Forms.Application]::Run($form)

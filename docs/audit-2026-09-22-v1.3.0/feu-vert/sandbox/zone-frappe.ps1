# Ouvert par smoke-amcf.ps1 (S6) dans Windows Sandbox : une zone de saisie pour juger la frappe
# AltGr. Elle remplace le Bloc-notes, absent du Sandbox de Windows 11 24H2 (26100, essai du 24/10).
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$form = New-Object System.Windows.Forms.Form
$form.Text = 'S6 - frappe AZERTY Global'
$form.Width = 640
$form.Height = 320
$form.StartPosition = 'CenterScreen'
$form.TopMost = $true
$box = New-Object System.Windows.Forms.TextBox
$box.Multiline = $true
$box.Dock = 'Fill'
$box.Font = New-Object System.Drawing.Font('Segoe UI', 24)
$form.Controls.Add($box)
$form.Add_Shown({ $form.Activate(); $box.Focus() })
[void]$form.ShowDialog()

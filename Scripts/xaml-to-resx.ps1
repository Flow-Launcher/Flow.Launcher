$items = Get-ChildItem "*.xaml"
New-Alias resgen "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\ResGen.exe"

foreach ($item in $items) {
    [xml]$content = Get-Content $item.FullName
    $txtfilePath = $item.Name.Substring(0, $item.Name.Length - 5) + ".txt"
    $resxfilePath = "Resource." + $item.Name.Substring(0, $item.Name.Length - 5) + ".resx"
    New-Item $filePath -Force
    foreach ($pair in $content.ResourceDictionary.String) {
        $key = $pair.Key
        $text = $pair.InnerText
        Add-Content $filePath "$key=$text"
    }
    resgen $filePath $resxfilePath
    Remove-Item $txtfilePath
}
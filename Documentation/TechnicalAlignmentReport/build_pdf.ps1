# Rebuilds the PDF from the HTML source using Microsoft Edge (headless).
$d = $PSScriptRoot
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
& $edge --headless=new --disable-gpu --no-pdf-header-footer "--print-to-pdf=$d\Code_Escape_Technical_Alignment_Report.pdf" "file:///$($d.Replace('\','/'))/Code_Escape_Technical_Alignment_Report.html"

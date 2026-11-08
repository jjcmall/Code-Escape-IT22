# Rebuilds the PDF from the HTML source using Microsoft Edge (headless).
# Rename the result to Lastname_GroupNumber_FirstRelease.pdf before submitting.
$d = $PSScriptRoot
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
& $edge --headless=new --disable-gpu --no-pdf-header-footer "--print-to-pdf=$d\Code_Escape_First_Release_Activity.pdf" "file:///$($d.Replace('\','/'))/Code_Escape_First_Release_Activity.html"

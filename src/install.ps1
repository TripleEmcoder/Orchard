param($installPath, $toolsPath, $package, $project)

#based on https://gist.github.com/2173751

$webConfig = $project.ProjectItems | where { $_.Name -eq "Web.config" }
$override = $project.ProjectItems | where { $_.Name -eq "override" }
$overrideWebConfig = $override.ProjectItems | where { $_.Name -eq "Web.config" }

if ($webConfig) {
    $overrideWebConfig.Open()
    $overrideWebConfig.Document.Activate()
    $overrideWebConfig.Document.Selection.SelectAll()
    $overrideWebConfigText = $overrideWebConfig.Document.Selection.Text;
    $webConfig.Open();
	$webConfig.Document.Activate()
	$webConfig.Document.Selection.SelectAll()
	$webConfig.Document.Selection.Insert($overrideWebConfigText)
	$webConfig.Document.Selection.StartOfDocument()
	$webConfig.Document.Close(0)
}

$overrideWebConfig.Delete()
$override.Delete()

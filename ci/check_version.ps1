[xml]$doc = Get-Content -Path ./Directory.Build.props
$version = $doc.SelectSingleNode('//Version').InnerText
$tag = $env:BUILD_SOURCEBRANCH.substring(1)

if ($tag -ne $version) {
    Write-Error "The tagged version does not equal the projects' version: $tag != $version"
    exit 1
} else {
    Write-Output "Version read from Directory.Build.props: $version"
}

# https://github.com/semver/semver/issues/232#issuecomment-546728483
$semver = [regex]@'
(?inx)
^
(0|[1-9]\d*)\.
(0|[1-9]\d*)\.
(0|[1-9]\d*)
(-([a-z-][\da-z-]+|[\da-z-]+[a-z-][\da-z-]*|0|[1-9]\d*)(\.([a-z-][\da-z-]+|[\da-z-]+[a-z-][\da-z-]*|0|[1-9]\d*))*)?
(\+[\da-z-]+(\.[\da-z-]+)*)?
$
'@

if (!$semver.IsMatch($tag)) {
    Write-Error "Version $tag is not SemVer 2.0.0 compliant!"
    exit 1
}

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$outputPath = Join-Path $PSScriptRoot 'DOCKER_DEPLOYMENT_GUIDE.docx'
$stagingPath = Join-Path $env:TEMP 'socialmediaweb-docker-guide-docx'
if (Test-Path $stagingPath) { Remove-Item -Recurse -Force $stagingPath }
New-Item -ItemType Directory -Path $stagingPath | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingPath '_rels') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingPath 'docProps') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingPath 'word') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $stagingPath 'word\_rels') | Out-Null

function Escape-Xml([string]$Text) {
    [System.Security.SecurityElement]::Escape($Text)
}

function Add-Paragraph([string]$Text, [string]$Style = 'Normal', [int]$Indent = 0) {
    $indentXml = if ($Indent -gt 0) { "<w:ind w:left="$Indent"/>" } else { '' }
    "<w:p><w:pPr><w:pStyle w:val="$Style"/>$indentXml</w:pPr><w:r><w:t xml:space="preserve">$(Escape-Xml $Text)</w:t></w:r></w:p>"
}

$content = @()
$content += Add-Paragraph 'SocialMediaWeb Docker Deployment Guide' 'Title'
$content += Add-Paragraph 'Combined React Frontend and ASP.NET Core Backend' 'Subtitle'
$content += Add-Paragraph 'This guide explains the Docker work completed for SocialMediaWeb and provides the repeatable process for building, validating, replacing, and publishing one application image. The React frontend and ASP.NET Core backend are packaged together, so one HTTP container serves the interface, API, static media, and the development Swagger endpoint.'

$content += Add-Paragraph 'Deployment summary' 'Heading1'
$content += Add-Paragraph 'Application image: ramp1712/socialmediaweb:latest'
$content += Add-Paragraph 'Local image ID: sha256:d168aa28a2a5e74ec09eb7a138256cdf672595b374cb47cc886062f6795d5f40'
$content += Add-Paragraph 'Application address: http://localhost:8080'
$content += Add-Paragraph 'Core Compose services: app and sqlserver'
$content += Add-Paragraph 'Persistent upload volumes: app-images and app-posts'
$content += Add-Paragraph 'Optional service: genai profile using gRPC port 50051'
$content += Add-Paragraph 'The repository configuration, local cleanup, combined build, and smoke test are complete. The Docker Hub latest tag is ready for publication after explicit external publish approval.'

$content += Add-Paragraph 'Combined image architecture' 'Heading1'
$content += Add-Paragraph 'The application image is deliberately one deployable unit for the frontend and backend. SQL Server remains a separate infrastructure dependency because it requires independent persistent storage and lifecycle management. The optional GenAI service also remains separate because it has Python and NVIDIA runtime requirements.'
$content += Add-Paragraph 'Frontend source  ->  Node 22 build  ->  Vite dist files' 'Code' 300
$content += Add-Paragraph 'Backend source   ->  .NET 10 publish ->  ASP.NET Core application' 'Code' 300
$content += Add-Paragraph 'Vite dist files  ->  Backend wwwroot ->  final ASP.NET runtime image' 'Code' 300
$content += Add-Paragraph 'Browser request  ->  port 8080       ->  static UI or API controller' 'Code' 300
$content += Add-Paragraph 'The final stage contains published .NET binaries and compiled frontend assets. It does not contain the frontend Node modules, frontend source, or the .NET SDK build layer.'

$content += Add-Paragraph 'Dockerfile process' 'Heading1'
$content += Add-Paragraph 'Frontend build stage' 'Heading2'
$content += Add-Paragraph 'The frontend stage uses node 22 alpine. It copies package manifests first, runs npm ci for deterministic dependency installation, copies the frontend source, and runs npm run build. The Dockerfile declares VITE_API_ORIGIN as an empty build argument so the Vite build replaces the development HTTPS URL with same origin paths. Browser calls such as /api therefore reach the backend in the same container.'
$content += Add-Paragraph 'Backend publish stage' 'Heading2'
$content += Add-Paragraph 'The backend stage uses the .NET 10 SDK. It restores the SocialMediaWeb project, copies backend source, copies the Vite dist output into Backend wwwroot, and publishes the API in Release configuration. This ensures that the compiled frontend is included in the published web root.'
$content += Add-Paragraph 'Runtime stage' 'Heading2'
$content += Add-Paragraph 'The final stage uses the smaller .NET 10 ASP.NET runtime image. It exposes port 8080, sets ASPNETCORE_URLS to http://+:8080, and starts SocialMediaWeb.dll. OCI metadata identifies the result as the combined SocialMediaWeb frontend and backend image.'

$content += Add-Paragraph 'Application serving behavior' 'Heading1'
$content += Add-Paragraph 'The backend uses wwwroot as its web root. UseDefaultFiles sends index.html for the site root, UseStaticFiles serves Vite assets and uploaded media, and controllers handle API requests. A fallback sends client-side routes such as /login to index.html while paths under /api, /Images, /posts, and /swagger retain normal 404 behavior when no matching endpoint or file exists.'

$content += Add-Paragraph 'Compose configuration' 'Heading1'
$content += Add-Paragraph 'The Compose file now uses app as the only application service. It builds from the repository root Dockerfile and tags the result with SOCIALMEDIA_IMAGE, which defaults to ramp1712/socialmediaweb:latest. The app listens on host port 8080 and receives its SQL connection string, JWT values, and optional GenAI address through environment variables.'
$content += Add-Paragraph 'Service app: combined frontend and backend image, host port 8080 to container port 8080.'
$content += Add-Paragraph 'Service sqlserver: SQL Server 2022 Developer dependency, host port 1433 to container port 1433.'
$content += Add-Paragraph 'Service genai profile: optional Python gRPC service, internal port 50051.'
$content += Add-Paragraph 'Uploaded images and posts are mounted separately at /app/wwwroot/Images and /app/wwwroot/posts. This preserves uploads across application-container replacement without masking the compiled frontend files in wwwroot. The prior backend-wwwroot volume is intentionally not deleted because it may contain existing user data; migrate needed uploads deliberately before deleting that old volume.'

$content += Add-Paragraph 'Build validation and replacement process' 'Heading1'
$content += Add-Paragraph 'Preparation' 'Heading2'
$content += Add-Paragraph 'Copy-Item .env.example .env' 'Code' 300
$content += Add-Paragraph 'docker compose config --quiet' 'Code' 300
$content += Add-Paragraph 'Set a strong SQL Server password and JWT secret in .env before running a real environment. The .dockerignore file excludes .env from the build context, preventing local secrets from being sent to Docker during image builds.'
$content += Add-Paragraph 'Removal of old local application images' 'Heading2'
$content += Add-Paragraph 'docker rm --force musing_ellis' 'Code' 300
$content += Add-Paragraph 'docker image rm socialmediaweb:latest socialmediaweb-backend:latest socialmediaweb-frontend:latest' 'Code' 300
$content += Add-Paragraph 'The previous unbound combined-image container and the old combined, backend, and frontend images were removed. SQL Server volumes and unrelated Docker Desktop images were not modified.'
$content += Add-Paragraph 'Rebuild and local smoke test' 'Heading2'
$content += Add-Paragraph 'docker build --pull --quiet --tag ramp1712/socialmediaweb:latest .' 'Code' 300
$content += Add-Paragraph 'docker run --detach --name socialmediaweb-smoke --publish 18080:8080 ramp1712/socialmediaweb:latest' 'Code' 300
$content += Add-Paragraph 'Invoke-WebRequest http://127.0.0.1:18080/' 'Code' 300
$content += Add-Paragraph 'docker rm --force socialmediaweb-smoke' 'Code' 300
$content += Add-Paragraph 'The final verified local image is d168aa28a2a5 and Docker Desktop lists it at approximately 387 MB. The smoke test confirmed a 200 response for the root UI, a 200 response for a client-side route, a 200 response for a bundled JavaScript asset, and a 404 response for an unknown API path.'

$content += Add-Paragraph 'Docker Hub publication process' 'Heading1'
$content += Add-Paragraph 'The Docker Hub destination is ramp1712/socialmediaweb:latest. Before the build, a manifest inspection reported that this latest tag did not exist. The command below creates or updates that tag. Updating latest moves the tag reference to the new image manifest; it does not alter SQL Server or GenAI dependency images.'
$content += Add-Paragraph 'docker push ramp1712/socialmediaweb:latest' 'Code' 300
$content += Add-Paragraph 'docker manifest inspect ramp1712/socialmediaweb:latest --verbose' 'Code' 300
$content += Add-Paragraph 'After publication, a machine can use docker pull ramp1712/socialmediaweb:latest and docker compose up to run the image with its SQL Server dependency. For production releases, also create an immutable version tag such as ramp1712/socialmediaweb:2026-09-07 and deploy that version tag rather than relying solely on latest.'

$content += Add-Paragraph 'Operational commands' 'Heading1'
$content += Add-Paragraph 'docker compose up --build' 'Code' 300
$content += Add-Paragraph 'docker compose --profile genai up --build' 'Code' 300
$content += Add-Paragraph 'docker compose logs -f app' 'Code' 300
$content += Add-Paragraph 'docker compose down' 'Code' 300
$content += Add-Paragraph 'Use docker compose down to stop containers without deleting persistent volumes. Use docker compose down -v only when the SQL Server database and uploaded-media volumes are intentionally being discarded.'

$content += Add-Paragraph 'Security and maintenance' 'Heading1'
$content += Add-Paragraph 'Do not commit .env files or production JWT and SQL passwords. Replace the example credentials before any environment is exposed beyond local development. Keep the image current by rebuilding with --pull, reviewing base image updates, and republishing after application changes. The application is served over HTTP for local Docker Desktop use; put it behind HTTPS termination and review the permissive CORS policy before a public deployment.'

$content += Add-Paragraph 'Files changed for this deployment' 'Heading1'
$content += Add-Paragraph 'Dockerfile builds the Vite frontend, publishes the .NET backend, and produces the combined runtime image.'
$content += Add-Paragraph 'Backend Program cs adds default-document handling and SPA fallback behavior for combined hosting.'
$content += Add-Paragraph 'docker-compose yml runs one app service with SQL Server and optional GenAI dependencies.'
$content += Add-Paragraph 'env example sets the combined image name and updates localhost settings to port 8080.'
$content += Add-Paragraph 'dockerignore excludes local environment values and backend tests from the build context.'
$content += Add-Paragraph 'README md documents the combined image and Docker Hub pull workflow.'

$documentXml = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>$($content -join '')<w:sectPr><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1280" w:right="1440" w:bottom="1280" w:left="1440" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr></w:body></w:document>"
$stylesXml = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Aptos" w:hAnsi="Aptos"/><w:sz w:val="21"/><w:color w:val="000000"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="140" w:line="280" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style><w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="100"/></w:pPr><w:rPr><w:rFonts w:ascii="Aptos Display" w:hAnsi="Aptos Display"/><w:sz w:val="48"/><w:color w:val="000000"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="300"/></w:pPr><w:rPr><w:sz w:val="26"/><w:color w:val="000000"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="Heading 1"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="260" w:after="100"/></w:pPr><w:rPr><w:sz w:val="32"/><w:b/><w:color w:val="000000"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="Heading 2"/><w:basedOn w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:spacing w:before="180" w:after="70"/></w:pPr><w:rPr><w:sz w:val="25"/><w:b/><w:color w:val="000000"/></w:rPr></w:style><w:style w:type="paragraph" w:styleId="Code"><w:name w:val="Code"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="0" w:line="220" w:lineRule="auto"/></w:pPr><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="17"/><w:color w:val="000000"/></w:rPr></w:style></w:styles>"
$contentTypes = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/></Types>"
$rootRelationships = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/></Relationships>"
$docRelationships = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>"
$coreProperties = "<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><dc:title>SocialMediaWeb Docker Deployment Guide</dc:title><dc:creator>SocialMediaWeb</dc:creator><dcterms:created xsi:type="dcterms:W3CDTF">2026-09-07T00:00:00Z</dcterms:created></cp:coreProperties>"

$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $stagingPath '[Content_Types].xml'), $contentTypes, $utf8)
[System.IO.File]::WriteAllText((Join-Path $stagingPath '_rels\.rels'), $rootRelationships, $utf8)
[System.IO.File]::WriteAllText((Join-Path $stagingPath 'docProps\core.xml'), $coreProperties, $utf8)
[System.IO.File]::WriteAllText((Join-Path $stagingPath 'word\document.xml'), $documentXml, $utf8)
[System.IO.File]::WriteAllText((Join-Path $stagingPath 'word\styles.xml'), $stylesXml, $utf8)
[System.IO.File]::WriteAllText((Join-Path $stagingPath 'word\_rels\document.xml.rels'), $docRelationships, $utf8)
if (Test-Path $outputPath) { Remove-Item -Force $outputPath }
[System.IO.Compression.ZipFile]::CreateFromDirectory($stagingPath, $outputPath)
[xml](Get-Content -Raw (Join-Path $stagingPath 'word\document.xml')) | Out-Null
"DOCX=$outputPath"

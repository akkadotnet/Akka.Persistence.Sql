# Azure Pipelines Build Files
These `.yaml` files are used by Windows Azure DevOps Pipelines to help execute the following types of builds:

- Pull request validation on Linux (Mono / .NET Core)
- Pull request validation on Windows (.NET Framework / .NET Core)
- NuGet releases with automatic release notes posted to a Github Release repository.

**NOTE**: releases are published by the GitHub Actions workflow [`.github/workflows/publish_nuget.yml`](../.github/workflows/publish_nuget.yml) when a tag is pushed. It uses NuGet trusted publishing (no stored API key): the nuget.org trusted publishing policy must name this repository, the `publish_nuget.yml` workflow, and the `publish` environment, and the repository needs a `NUGET_USER` secret with the policy owner's nuget.org username. To publish an existing tag, run the workflow manually and enter the tag.

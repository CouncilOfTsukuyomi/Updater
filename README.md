# Updater

A lightweight .NET application for installing or updating software from a GitHub repository.

This will be used by most of the Council Of Tsukuyomi projects.

## Overview

• Checks for any existing instance of the Updater to prevent duplicate runs.  
• Interpret command-line arguments to locate, download, and install files from a specified GitHub source.  
• Optionally runs a specified program immediately after installation.  
• Can log errors and exceptions to Sentry based on a command-line flag.

## Usage

The Updater requires five arguments in this order:

1. VersionNumber  
   The version number of the software you want to install or update.

2. GitHubRepo  
   The GitHub repository in the format "Owner/Repository" where the files are hosted.

3. InstallationPath  
   The directory on your machine where the files should be installed.

4. enableSentry 
   • "true" to enable Sentry error logging, or  
   • "false" to disable it.

5. ProgramToRunAfterInstallation  
   Optional. The executable or script to launch after installation completes.

### Example

```bash
Updater.exe 1.0.0 MyAccount/MyRepository "C:\MyApp" true MyApp.exe
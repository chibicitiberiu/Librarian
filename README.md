# Librarian

**Note: this project is early in development, and far from being ready to use.**

The Librarian is a tool that can be used to curate and search through your data collection.

## The vision

In the "Librarian", the data collection is simply a big folder on the server. The goal is to provide a user interface not unlike a file manager that allows people to browse and manage their data. In addition to what a standard file manager offers, Librarian also collects and displays metadata about each file or folder, as well as allowing users to add their own metadata. Another part of the goal is to also allow users to add formatted text, allowing the creation of a hierarchical "wiki" based on the data.

Another important feature is indexing. The goal of Librarian is to index the entire data collection (metadata and content where possible), and provide tools for searching through that index.

## Development

The project is created in ASP.NET using .NET 6.0. There is a small utilty written in C++ that can interact with libavformat to collect metadata.

For the web UI, I'm simply using vanilla JS. The theme is inspired by Bluecurve, an old Fedora theme.

### Setting up a dev environment

* Install Visual Studio 2022 with the ASP.NET and Linux C++ workloads. I'm not sure if you need anything extra for working with WSL.
* Enable WSL and install a distro
* Install PostgreSQL, create a database and a user/password with full rights to that database
* Open the meta-cli folder in Visual Studio. Note that this is a C++ cmake project.
* Build the meta-cli project (make sure you build it for Linux in the Ubuntu WSL instance).
* Open the main solution - New Librarian.sln.
* Configure appsettings.json (see below)
* Run the migrations (using MigratePostgres.cmd) to create the tables in the database
* Run in WSL

### Setting up appsettings.json
In the Librarian project, there is an "appsettings.json" file which contains the application's configuration. There are some things that need to be set up here for it to start up properly.
* the `BaseDirectory` is the root folder of the data collection. I recommend creating a temporary folder and dumping some media files in there to be used for testing.
* `ConnectionsStrings.DB` is the connection string to your postgres database. Note: when creating migrations, you will also need to set the connection string in PostgresDatabaseContext.cs.
* `Languages` contains a list of languages to be used for full text search. Currently, this is implemented using PostgreSQL's full text search features. Use this query to find what languages are supported in your SQL instance: `SELECT cfgname FROM pg_ts_config;`. More can be added, read about it in the Postgres documentation.
* `MetadataCliPath` set this to the path of the built meta-cli tool.

## Current state/screenshots

The file browser looks like this:
![Screenshot 2024-01-27 170504](https://github.com/chibicitiberiu/Librarian/assets/5184913/dbb79bd2-b625-47ca-900c-8f6180f4e72d)

Some things I want to change:

* add a "title", "description" section that can be edited (like a wiki).

Metadata editor:
![Screenshot 2024-01-27 170754](https://github.com/chibicitiberiu/Librarian/assets/5184913/e92c1c6a-317f-4b2b-8f98-bdc926ca3376)

Search is not yet implemented.

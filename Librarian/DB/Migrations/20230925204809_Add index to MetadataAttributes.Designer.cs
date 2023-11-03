﻿// <auto-generated />
using System;
using Librarian.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace Librarian.DB.Migrations
{
    [DbContext(typeof(PostgresDatabaseContext))]
    [Migration("20230925204809_Add index to MetadataAttributes")]
    partial class AddindextoMetadataAttributes
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Librarian.Model.BlobMetadata", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AttributeId")
                        .HasColumnType("integer");

                    b.Property<bool>("Editable")
                        .HasColumnType("boolean");

                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<int>("ProviderId")
                        .HasColumnType("integer");

                    b.Property<byte[]>("Value")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.HasKey("Id");

                    b.HasIndex("AttributeId");

                    b.HasIndex("FileId");

                    b.ToTable("BlobMetadata");
                });

            modelBuilder.Entity("Librarian.Model.DateMetadata", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AttributeId")
                        .HasColumnType("integer");

                    b.Property<bool>("Editable")
                        .HasColumnType("boolean");

                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<int>("ProviderId")
                        .HasColumnType("integer");

                    b.Property<DateTimeOffset>("Value")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AttributeId");

                    b.HasIndex("FileId");

                    b.ToTable("DateMetadata");
                });

            modelBuilder.Entity("Librarian.Model.FloatMetadata", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AttributeId")
                        .HasColumnType("integer");

                    b.Property<bool>("Editable")
                        .HasColumnType("boolean");

                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<int>("ProviderId")
                        .HasColumnType("integer");

                    b.Property<double>("Value")
                        .HasColumnType("double precision");

                    b.HasKey("Id");

                    b.HasIndex("AttributeId");

                    b.HasIndex("FileId");

                    b.ToTable("FloatMetadata");
                });

            modelBuilder.Entity("Librarian.Model.IndexedFile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("Created")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("IndexLastUpdated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("Modified")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("NeedsUpdating")
                        .HasColumnType("boolean");

                    b.Property<string>("Path")
                        .IsRequired()
                        .HasMaxLength(4096)
                        .HasColumnType("character varying(4096)");

                    b.Property<long?>("Size")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("Path")
                        .IsUnique();

                    b.ToTable("IndexedFiles");
                });

            modelBuilder.Entity("Librarian.Model.IndexedFileContents", b =>
                {
                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<string>("Content")
                        .HasColumnType("text");

                    b.Property<NpgsqlTsVector>("ContentSearch")
                        .HasColumnType("tsvector");

                    b.HasKey("FileId");

                    b.ToTable("IndexedFileContents");
                });

            modelBuilder.Entity("Librarian.Model.IntegerMetadata", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AttributeId")
                        .HasColumnType("integer");

                    b.Property<bool>("Editable")
                        .HasColumnType("boolean");

                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<int>("ProviderId")
                        .HasColumnType("integer");

                    b.Property<long>("Value")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("AttributeId");

                    b.HasIndex("FileId");

                    b.ToTable("IntegerMetadata");
                });

            modelBuilder.Entity("Librarian.Model.MetadataAttribute", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Grouping")
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(120)
                        .HasColumnType("character varying(120)");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("Grouping", "Name", "Type")
                        .IsUnique();

                    b.ToTable("MetadataAttributes");
                });

            modelBuilder.Entity("Librarian.Model.TextMetadata", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("AttributeId")
                        .HasColumnType("integer");

                    b.Property<bool>("Editable")
                        .HasColumnType("boolean");

                    b.Property<int>("FileId")
                        .HasColumnType("integer");

                    b.Property<int>("ProviderId")
                        .HasColumnType("integer");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<NpgsqlTsVector>("ValueSearch")
                        .HasColumnType("tsvector");

                    b.HasKey("Id");

                    b.HasIndex("AttributeId");

                    b.HasIndex("FileId");

                    b.ToTable("TextMetadata");
                });

            modelBuilder.Entity("Librarian.Model.BlobMetadata", b =>
                {
                    b.HasOne("Librarian.Model.MetadataAttribute", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithMany("BlobMetadata")
                        .HasForeignKey("FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.DateMetadata", b =>
                {
                    b.HasOne("Librarian.Model.MetadataAttribute", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithMany("DateMetadata")
                        .HasForeignKey("FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.FloatMetadata", b =>
                {
                    b.HasOne("Librarian.Model.MetadataAttribute", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithMany("FloatMetadata")
                        .HasForeignKey("FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.IndexedFileContents", b =>
                {
                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithOne("Contents")
                        .HasForeignKey("Librarian.Model.IndexedFileContents", "FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.IntegerMetadata", b =>
                {
                    b.HasOne("Librarian.Model.MetadataAttribute", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithMany("IntegerMetadata")
                        .HasForeignKey("FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.TextMetadata", b =>
                {
                    b.HasOne("Librarian.Model.MetadataAttribute", "Attribute")
                        .WithMany()
                        .HasForeignKey("AttributeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Librarian.Model.IndexedFile", "File")
                        .WithMany("TextMetadata")
                        .HasForeignKey("FileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Attribute");

                    b.Navigation("File");
                });

            modelBuilder.Entity("Librarian.Model.IndexedFile", b =>
                {
                    b.Navigation("BlobMetadata");

                    b.Navigation("Contents");

                    b.Navigation("DateMetadata");

                    b.Navigation("FloatMetadata");

                    b.Navigation("IntegerMetadata");

                    b.Navigation("TextMetadata");
                });
#pragma warning restore 612, 618
        }
    }
}

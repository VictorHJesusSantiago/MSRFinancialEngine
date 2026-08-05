using System;
using MSRFinancialEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MSRFinancialEngine.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(FinancialEngineDbContext))]
    [Migration("20260731225616_AddUserPasswordHash")]
    partial class AddUserPasswordHash
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.ApplicationUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<decimal?>("ApprovalLimitAmount")
                        .HasColumnType("numeric(18,2)");

                    b.Property<Guid?>("CompanyId")
                        .HasColumnType("uuid");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Role")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CompanyId");

                    b.HasIndex("Email")
                        .IsUnique();

                    b.ToTable("users", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.ApprovalDecision", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("DecidedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Decision")
                        .HasColumnType("integer");

                    b.Property<Guid>("DivergenceId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("MatchedTransactionId")
                        .HasColumnType("uuid");

                    b.Property<string>("Notes")
                        .HasColumnType("text");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("DivergenceId");

                    b.HasIndex("UserId");

                    b.ToTable("approval_decisions", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.AuditEvent", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("DetailsJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("EntityId")
                        .HasColumnType("uuid");

                    b.Property<string>("EntityType")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTime>("Timestamp")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("Timestamp");

                    b.HasIndex("EntityType", "EntityId");

                    b.ToTable("audit_events", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("AccountIdentifier")
                        .HasColumnType("text");

                    b.Property<decimal>("Amount")
                        .HasColumnType("numeric(18,2)");

                    b.Property<Guid>("CompanyId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid?>("RawTransactionId")
                        .HasColumnType("uuid");

                    b.Property<bool>("Reconciled")
                        .HasColumnType("boolean");

                    b.Property<string>("ReferenceDoc")
                        .HasColumnType("text");

                    b.Property<Guid>("SourceId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("TransactionDate")
                        .HasColumnType("timestamp without time zone");

                    b.HasKey("Id");

                    b.HasIndex("RawTransactionId")
                        .IsUnique();

                    b.HasIndex("CompanyId", "Hash");

                    b.HasIndex("CompanyId", "Reconciled");

                    b.ToTable("canonical_transactions", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Company", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("BaseCurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("TaxId")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)");

                    b.HasKey("Id");

                    b.HasIndex("TaxId");

                    b.ToTable("companies", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Divergence", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("AssignedToUserId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Reason")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("ResolvedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<Guid>("TransactionId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("AssignedToUserId");

                    b.HasIndex("Status");

                    b.HasIndex("TransactionId")
                        .IsUnique();

                    b.ToTable("divergences", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.ExchangeRate", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("BaseCurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<string>("CurrencyCode")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("character varying(3)");

                    b.Property<DateOnly>("Date")
                        .HasColumnType("date");

                    b.Property<decimal>("RateToBase")
                        .HasColumnType("numeric(18,6)");

                    b.HasKey("Id");

                    b.HasIndex("CurrencyCode", "BaseCurrencyCode", "Date")
                        .IsUnique();

                    b.ToTable("exchange_rates", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.MatchCandidate", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("RuleId")
                        .HasColumnType("uuid");

                    b.Property<double>("Score")
                        .HasColumnType("double precision");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<Guid>("TransactionAId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("TransactionBId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("RuleId");

                    b.HasIndex("TransactionAId");

                    b.HasIndex("TransactionBId");

                    b.ToTable("match_candidates", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.MatchingRule", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<Guid>("CompanyId")
                        .HasColumnType("uuid");

                    b.Property<string>("ConfigJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Priority")
                        .HasColumnType("integer");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CompanyId", "Active", "Priority");

                    b.ToTable("matching_rules", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.RawTransaction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("ImportedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("Normalized")
                        .HasColumnType("boolean");

                    b.Property<string>("PayloadJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("SourceId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("SourceId");

                    b.ToTable("raw_transactions", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Source", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<Guid>("CompanyId")
                        .HasColumnType("uuid");

                    b.Property<string>("ConfigJson")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<int>("Type")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CompanyId");

                    b.ToTable("sources", (string)null);
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.ApprovalDecision", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.Divergence", "Divergence")
                        .WithMany("Decisions")
                        .HasForeignKey("DivergenceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MSRFinancialEngine.Domain.Entities.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Divergence");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.Company", "Company")
                        .WithMany("Transactions")
                        .HasForeignKey("CompanyId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MSRFinancialEngine.Domain.Entities.RawTransaction", "RawTransaction")
                        .WithOne("CanonicalTransaction")
                        .HasForeignKey("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", "RawTransactionId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("Company");

                    b.Navigation("RawTransaction");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Divergence", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.ApplicationUser", "AssignedTo")
                        .WithMany()
                        .HasForeignKey("AssignedToUserId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", "Transaction")
                        .WithOne("Divergence")
                        .HasForeignKey("MSRFinancialEngine.Domain.Entities.Divergence", "TransactionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AssignedTo");

                    b.Navigation("Transaction");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.MatchCandidate", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.MatchingRule", "Rule")
                        .WithMany("MatchCandidates")
                        .HasForeignKey("RuleId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", "TransactionA")
                        .WithMany("MatchCandidatesAsA")
                        .HasForeignKey("TransactionAId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", "TransactionB")
                        .WithMany("MatchCandidatesAsB")
                        .HasForeignKey("TransactionBId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Rule");

                    b.Navigation("TransactionA");

                    b.Navigation("TransactionB");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.MatchingRule", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.Company", "Company")
                        .WithMany("MatchingRules")
                        .HasForeignKey("CompanyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Company");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.RawTransaction", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.Source", "Source")
                        .WithMany("RawTransactions")
                        .HasForeignKey("SourceId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Source");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Source", b =>
                {
                    b.HasOne("MSRFinancialEngine.Domain.Entities.Company", "Company")
                        .WithMany("Sources")
                        .HasForeignKey("CompanyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Company");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.CanonicalTransaction", b =>
                {
                    b.Navigation("Divergence");

                    b.Navigation("MatchCandidatesAsA");

                    b.Navigation("MatchCandidatesAsB");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Company", b =>
                {
                    b.Navigation("MatchingRules");

                    b.Navigation("Sources");

                    b.Navigation("Transactions");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Divergence", b =>
                {
                    b.Navigation("Decisions");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.MatchingRule", b =>
                {
                    b.Navigation("MatchCandidates");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.RawTransaction", b =>
                {
                    b.Navigation("CanonicalTransaction");
                });

            modelBuilder.Entity("MSRFinancialEngine.Domain.Entities.Source", b =>
                {
                    b.Navigation("RawTransactions");
                });
#pragma warning restore 612, 618
        }
    }
}

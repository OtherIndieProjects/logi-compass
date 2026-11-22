namespace AAALogistics.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AAA Logistics";

    public string InternalAudience { get; set; } = "aaa-logistics-internal";

    public string PortalAudience { get; set; } = "aaa-logistics-portal";

    public string StepUpAudience { get; set; } = "aaa-logistics-stepup";

    public int AccessTokenMinutes { get; set; } = 15;

    public int StepUpMinutes { get; set; } = 5;

    public int RefreshTokenDays { get; set; } = 14;

    public string SigningKeyPath { get; set; } =
        Path.Combine("Data", "keys", "jwt.key");
}

public sealed class ListenerOptions
{
    public const string SectionName = "Listeners";

    public string InternalUrl { get; set; } = "http://127.0.0.1:5088";

    public string PortalUrl { get; set; } = "http://127.0.0.1:5089";
}

public sealed class LegalOptions
{
    public const string SectionName = "Legal";

    public bool RequireLegalTexts { get; set; }

    public string CurrentVersion { get; set; } = string.Empty;
}

public enum EmailMode
{
    Disabled,
    File,
    Smtp,
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailMode Mode { get; set; } = EmailMode.Disabled;

    public string FileDirectory { get; set; } =
        Path.Combine("Data", "emails");

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public string? SmtpUserName { get; set; }

    public string? SmtpPassword { get; set; }

    public string? SmtpFrom { get; set; }

    public bool SmtpUseStartTls { get; set; } = true;
}

public sealed class VerifactuOptions
{
    public const string SectionName = "Verifactu";

    /// <summary>LocalChain (default) or AeatHttp.</summary>
    public string Mode { get; set; } = "LocalChain";

    public string? AeatBaseUrl { get; set; }

    public string? ClientCertificatePath { get; set; }
}

public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public bool RequireProofOfWork { get; set; }

    public int DifficultyBits { get; set; } = 16;
}

public sealed class FirstBootOptions
{
    public const string SectionName = "FirstBoot";

    public string AdminUserName { get; set; } = string.Empty;

    public string AdminPassword { get; set; } = string.Empty;
}

public sealed class PasswordHashingOptions
{
    public const string SectionName = "PasswordHashing";

    public int MemorySizeKb { get; set; } = 65_536;

    public int Iterations { get; set; } = 3;

    public int DegreeOfParallelism { get; set; } = 2;

    public int MaxConcurrentOperations { get; set; } = 2;
}

public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    public string PiiMasterKeyPath { get; set; } =
        Path.Combine("Data", "keys", "pii-master.key");

    /// <summary>Days to keep audit payloads discoverable for retention status.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>External checkpoint manifests, next to backups.</summary>
    public string CheckpointDirectory { get; set; } =
        Path.Combine("Data", "audit-checkpoints");

    public bool RetentionJobEnabled { get; set; } = true;

    public int RetentionPollHours { get; set; } = 24;
}

public sealed class BackupOptions
{
    public const string SectionName = "Backups";

    public string Directory { get; set; } =
        Path.Combine("Data", "backups");

    public string PgDumpPath { get; set; } = "pg_dump";

    public int TimeoutMinutes { get; set; } = 30;

    public bool AllowSqlFallback { get; set; }

    /// <summary>
    /// AES-256 key file (outside the dump) used to encrypt backup ciphertext.
    /// </summary>
    public string EncryptionKeyPath { get; set; } =
        Path.Combine("Data", "keys", "backup.key");
}

public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool AllowSeed { get; set; }
}

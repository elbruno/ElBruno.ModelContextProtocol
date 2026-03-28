// ============================================================================
// Tool Definitions — 120+ MCP tools across 12 domains
// Used by LLMDistillationMax to benchmark Mode 1 vs Mode 2 routing.
// ============================================================================

using ModelContextProtocol.Protocol;

static class ToolDefinitions
{
    public static Tool[] Build() =>
    [
        // ── Weather & Environment (10) ───────────────────────────────
        new() { Name = "get_current_weather", Description = "Retrieves current weather conditions for a specified location including temperature, humidity, wind speed, pressure, and sky conditions" },
        new() { Name = "get_weather_forecast", Description = "Gets a multi-day weather forecast for a location with daily highs, lows, precipitation probability, and conditions" },
        new() { Name = "get_weather_alerts", Description = "Retrieves active severe weather alerts, watches, and warnings for a specified geographic region" },
        new() { Name = "get_air_quality", Description = "Returns the Air Quality Index (AQI) and pollutant levels for a given location including PM2.5, PM10, and ozone" },
        new() { Name = "get_uv_index", Description = "Returns the current and forecasted UV index for a location along with sun protection recommendations" },
        new() { Name = "get_sunrise_sunset", Description = "Returns sunrise, sunset, dawn, and dusk times for a given location and date" },
        new() { Name = "get_historical_weather", Description = "Retrieves historical weather data for a location over a specified date range for climate analysis" },
        new() { Name = "get_pollen_count", Description = "Returns current pollen counts and allergy forecast for a location including grass, tree, and weed pollen levels" },
        new() { Name = "get_tide_info", Description = "Returns tide predictions and current tide levels for coastal locations and harbors" },
        new() { Name = "get_wind_map", Description = "Generates a wind speed and direction map for a region showing current atmospheric wind patterns" },

        // ── Email & Messaging (10) ───────────────────────────────────
        new() { Name = "send_email", Description = "Sends an email message with subject, body, recipients (to, cc, bcc), and optional file attachments via SMTP" },
        new() { Name = "read_inbox", Description = "Retrieves unread emails from the inbox with options to filter by sender, subject, date range, or importance" },
        new() { Name = "search_emails", Description = "Performs full-text search across the email archive matching keywords, phrases, sender, date range, and labels" },
        new() { Name = "delete_email", Description = "Permanently deletes an email message by its unique identifier or moves it to the trash folder" },
        new() { Name = "create_email_draft", Description = "Creates a new email draft that can be edited and sent later, with subject, body, and recipients" },
        new() { Name = "send_sms", Description = "Sends an SMS text message to a phone number with optional delivery confirmation and scheduling" },
        new() { Name = "send_slack_message", Description = "Posts a message to a Slack channel or direct message thread with optional attachments and formatting" },
        new() { Name = "send_teams_message", Description = "Sends a message to a Microsoft Teams channel or chat including rich text, images, and adaptive cards" },
        new() { Name = "list_email_folders", Description = "Lists all email folders and labels in the mailbox with message counts and unread counts" },
        new() { Name = "set_email_rule", Description = "Creates an email rule to automatically sort, label, forward, or delete incoming messages based on criteria" },

        // ── File System & Storage (10) ───────────────────────────────
        new() { Name = "read_file", Description = "Reads and returns the full text content of a file at the specified path with encoding detection" },
        new() { Name = "write_file", Description = "Writes or overwrites content to a file at the specified path, creating parent directories if needed" },
        new() { Name = "list_directory", Description = "Lists all files and subdirectories in a directory path with size, modification date, and permissions" },
        new() { Name = "search_files", Description = "Recursively searches the filesystem for files matching a glob pattern, name fragment, or containing text" },
        new() { Name = "copy_file", Description = "Copies a file or directory from source to destination path with optional overwrite and progress tracking" },
        new() { Name = "move_file", Description = "Moves or renames a file or directory from one path to another with conflict resolution options" },
        new() { Name = "delete_file", Description = "Deletes a file or directory permanently or moves it to the recycle bin with confirmation" },
        new() { Name = "get_file_info", Description = "Returns detailed metadata for a file including size, creation date, modified date, hash, and MIME type" },
        new() { Name = "compress_files", Description = "Compresses files and directories into a ZIP, TAR.GZ, or 7Z archive with compression level options" },
        new() { Name = "upload_to_cloud_storage", Description = "Uploads a local file to cloud storage (S3, Azure Blob, GCS) with progress tracking and checksum verification" },

        // ── Database & Data (10) ─────────────────────────────────────
        new() { Name = "query_database", Description = "Executes a read-only SQL SELECT query against a database and returns formatted tabular results with column types" },
        new() { Name = "insert_record", Description = "Inserts one or more new records into a database table with input validation and returns generated IDs" },
        new() { Name = "update_record", Description = "Updates existing database records matching specified conditions with new field values and returns affected row count" },
        new() { Name = "delete_record", Description = "Deletes database records matching specified conditions with optional soft-delete support and audit logging" },
        new() { Name = "list_tables", Description = "Lists all tables and views in a database schema with row counts, column summaries, and size information" },
        new() { Name = "describe_table", Description = "Returns the full schema definition of a database table including columns, types, constraints, and indexes" },
        new() { Name = "export_to_csv", Description = "Exports query results or an entire table to a CSV file with configurable delimiter, encoding, and headers" },
        new() { Name = "import_csv", Description = "Imports data from a CSV file into a database table with column mapping, type conversion, and error handling" },
        new() { Name = "run_migration", Description = "Executes a database schema migration script to create, alter, or drop tables, columns, and indexes" },
        new() { Name = "backup_database", Description = "Creates a full or incremental backup of a database to a specified location with compression and encryption" },

        // ── Calendar & Scheduling (10) ───────────────────────────────
        new() { Name = "create_calendar_event", Description = "Creates a new calendar event with title, date, time, duration, location, description, and attendee invitations" },
        new() { Name = "list_calendar_events", Description = "Lists calendar events within a specified date range with filtering by calendar, attendee, or keyword" },
        new() { Name = "update_calendar_event", Description = "Modifies an existing calendar event's details including time, location, description, or attendee list" },
        new() { Name = "delete_calendar_event", Description = "Deletes a calendar event by ID with options to notify attendees and handle recurring event instances" },
        new() { Name = "check_availability", Description = "Checks schedule availability for one or more people across a date range to find open meeting slots" },
        new() { Name = "create_reminder", Description = "Creates a time-based or location-based reminder with customizable notification settings and recurrence" },
        new() { Name = "set_recurring_event", Description = "Creates a recurring calendar event with flexible patterns: daily, weekly, monthly, or custom recurrence rules" },
        new() { Name = "get_timezone_info", Description = "Returns timezone details and current local time for a city or timezone identifier with DST information" },
        new() { Name = "sync_calendars", Description = "Synchronizes events between multiple calendar providers (Google, Outlook, Apple) with conflict resolution" },
        new() { Name = "create_scheduling_poll", Description = "Creates a poll for attendees to vote on preferred meeting times from a set of proposed slots" },

        // ── Math & Science (10) ──────────────────────────────────────
        new() { Name = "calculate_expression", Description = "Evaluates a mathematical expression supporting arithmetic, trigonometry, logarithms, and complex numbers" },
        new() { Name = "convert_units", Description = "Converts a value between measurement units including length, mass, temperature, volume, speed, and currency" },
        new() { Name = "calculate_statistics", Description = "Computes descriptive statistics for a dataset: mean, median, mode, standard deviation, variance, percentiles" },
        new() { Name = "solve_equation", Description = "Solves algebraic equations symbolically or numerically, including linear, quadratic, and systems of equations" },
        new() { Name = "calculate_compound_interest", Description = "Computes compound interest over time with principal, rate, compounding frequency, and regular contributions" },
        new() { Name = "matrix_operations", Description = "Performs matrix arithmetic: multiplication, inversion, determinant, eigenvalues, and decomposition" },
        new() { Name = "generate_random_numbers", Description = "Generates random numbers from specified distributions: uniform, normal, Poisson, exponential, or custom ranges" },
        new() { Name = "calculate_distance", Description = "Calculates the geographic distance between two coordinates using the Haversine formula with elevation support" },
        new() { Name = "chemical_formula_parser", Description = "Parses chemical formulas and returns molecular weight, elemental composition, and structural information" },
        new() { Name = "physics_calculator", Description = "Solves common physics problems: kinematics, force, energy, electricity, optics, and thermodynamics calculations" },

        // ── Translation & Language (10) ──────────────────────────────
        new() { Name = "translate_text", Description = "Translates text from a source language to a target language using neural machine translation with context awareness" },
        new() { Name = "detect_language", Description = "Detects the language of input text with confidence scores and returns ISO language codes" },
        new() { Name = "transliterate_text", Description = "Converts text from one script to another (e.g., Cyrillic to Latin, Kanji to Romaji) preserving pronunciation" },
        new() { Name = "check_grammar", Description = "Analyzes text for grammar, spelling, punctuation, and style errors with correction suggestions" },
        new() { Name = "summarize_text", Description = "Generates a concise summary of a long document preserving key points, themes, and conclusions" },
        new() { Name = "extract_keywords", Description = "Extracts the most important keywords and key phrases from text using NLP-based relevance scoring" },
        new() { Name = "analyze_sentiment", Description = "Performs sentiment analysis on text returning positive, negative, neutral scores and emotional tone categories" },
        new() { Name = "text_to_speech", Description = "Converts text to natural-sounding audio in multiple languages and voices with speed and pitch controls" },
        new() { Name = "speech_to_text", Description = "Transcribes audio or speech recordings to text with speaker diarization and punctuation restoration" },
        new() { Name = "dictionary_lookup", Description = "Looks up word definitions, synonyms, antonyms, etymology, pronunciation, and usage examples in multiple languages" },

        // ── Web & HTTP (10) ──────────────────────────────────────────
        new() { Name = "http_get", Description = "Sends an HTTP GET request to a URL and returns the response body, status code, and headers" },
        new() { Name = "http_post", Description = "Sends an HTTP POST request with a JSON or form body to a URL and returns the response" },
        new() { Name = "scrape_webpage", Description = "Extracts structured content from a webpage including text, links, images, and metadata using CSS selectors" },
        new() { Name = "check_url_status", Description = "Checks if a URL is reachable and returns HTTP status code, response time, redirect chain, and SSL info" },
        new() { Name = "download_file_url", Description = "Downloads a file from a URL to a local path with progress tracking, resume support, and checksum validation" },
        new() { Name = "shorten_url", Description = "Creates a shortened URL using a URL shortening service with optional custom alias and expiration" },
        new() { Name = "parse_html", Description = "Parses an HTML document and extracts elements using XPath or CSS selector queries returning structured data" },
        new() { Name = "whois_lookup", Description = "Performs a WHOIS lookup for a domain name returning registrar, registration dates, nameservers, and contact info" },
        new() { Name = "dns_lookup", Description = "Queries DNS records for a domain: A, AAAA, MX, CNAME, TXT, NS, and SOA records with TTL information" },
        new() { Name = "trace_route", Description = "Performs a network traceroute to a host showing each hop, latency, and geographic location of routers" },

        // ── DevOps & CI/CD (10) ──────────────────────────────────────
        new() { Name = "run_ci_pipeline", Description = "Triggers a CI/CD pipeline or workflow by name with optional parameters, branch, and environment selection" },
        new() { Name = "get_pipeline_status", Description = "Returns the current status of a CI/CD pipeline run including stage progress, logs, and duration" },
        new() { Name = "deploy_to_environment", Description = "Deploys an application version to a specified environment (dev, staging, production) with rollback options" },
        new() { Name = "docker_build", Description = "Builds a Docker container image from a Dockerfile with build arguments, caching, and multi-stage support" },
        new() { Name = "docker_push", Description = "Pushes a Docker image to a container registry (Docker Hub, ACR, ECR, GCR) with tag and manifest management" },
        new() { Name = "kubectl_apply", Description = "Applies Kubernetes manifests to a cluster to create, update, or delete pods, services, and deployments" },
        new() { Name = "terraform_plan", Description = "Runs Terraform plan to preview infrastructure changes showing resources to create, modify, or destroy" },
        new() { Name = "terraform_apply", Description = "Applies Terraform configuration changes to provision or update cloud infrastructure with state management" },
        new() { Name = "check_service_health", Description = "Performs health checks on deployed services returning status, uptime, response time, and error rates" },
        new() { Name = "rollback_deployment", Description = "Rolls back a deployment to a previous version with automatic health checking and traffic shifting" },

        // ── Security & Auth (10) ─────────────────────────────────────
        new() { Name = "generate_api_key", Description = "Generates a new API key with configurable permissions, expiration, rate limits, and scope restrictions" },
        new() { Name = "rotate_credentials", Description = "Rotates passwords, API keys, or certificates for a service with zero-downtime credential swapping" },
        new() { Name = "check_ssl_certificate", Description = "Inspects an SSL/TLS certificate for a domain returning issuer, expiration, chain validity, and cipher details" },
        new() { Name = "scan_vulnerabilities", Description = "Scans a codebase or container image for known security vulnerabilities with severity ratings and remediation advice" },
        new() { Name = "encrypt_data", Description = "Encrypts data using AES-256, RSA, or other algorithms with key management and initialization vector handling" },
        new() { Name = "decrypt_data", Description = "Decrypts previously encrypted data using the specified algorithm and decryption key or private key" },
        new() { Name = "hash_data", Description = "Computes cryptographic hashes (SHA-256, SHA-512, MD5, BLAKE3) of data or files for integrity verification" },
        new() { Name = "manage_secrets", Description = "Stores, retrieves, or rotates secrets in a vault (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)" },
        new() { Name = "audit_permissions", Description = "Audits user and service account permissions across systems to identify excessive access and policy violations" },
        new() { Name = "generate_jwt_token", Description = "Creates a signed JWT token with custom claims, issuer, audience, and expiration for authentication" },

        // ── Analytics & Monitoring (10) ──────────────────────────────
        new() { Name = "get_metrics", Description = "Retrieves time-series metrics (CPU, memory, requests, latency, error rate) for services and infrastructure" },
        new() { Name = "create_dashboard", Description = "Creates a monitoring dashboard with configurable widgets, charts, and real-time data visualizations" },
        new() { Name = "set_alert_rule", Description = "Configures an alerting rule that triggers notifications when metrics exceed thresholds or anomalies are detected" },
        new() { Name = "query_logs", Description = "Searches and filters application and infrastructure logs using structured queries with time range and severity" },
        new() { Name = "get_error_report", Description = "Generates an error report summarizing exceptions, stack traces, affected users, and occurrence frequency" },
        new() { Name = "track_event", Description = "Records a custom analytics event with properties and metrics for product usage tracking and funnels" },
        new() { Name = "generate_report", Description = "Generates a formatted analytics report with charts, tables, and insights from metrics and log data" },
        new() { Name = "get_uptime_status", Description = "Returns uptime percentage, incident history, and SLA compliance for monitored services and endpoints" },
        new() { Name = "trace_request", Description = "Retrieves a distributed trace for a request ID showing the full call chain across microservices with timing" },
        new() { Name = "forecast_capacity", Description = "Analyzes historical metrics to forecast future resource usage and capacity needs with confidence intervals" },

        // ── AI & ML (10) ─────────────────────────────────────────────
        new() { Name = "generate_image", Description = "Generates an image from a text description prompt using AI image generation models with size and style options" },
        new() { Name = "analyze_image", Description = "Analyzes an image using computer vision to identify objects, faces, text (OCR), scenes, and generate descriptions" },
        new() { Name = "classify_text", Description = "Classifies text into categories using a trained ML model with confidence scores for each predicted label" },
        new() { Name = "generate_embeddings", Description = "Generates vector embeddings for text, images, or other data for semantic search and similarity comparisons" },
        new() { Name = "train_model", Description = "Initiates training of a machine learning model with specified dataset, hyperparameters, and evaluation metrics" },
        new() { Name = "evaluate_model", Description = "Evaluates a trained ML model against a test dataset returning accuracy, precision, recall, F1, and confusion matrix" },
        new() { Name = "run_inference", Description = "Runs inference on a deployed ML model with input data and returns predictions with confidence scores" },
        new() { Name = "finetune_llm", Description = "Fine-tunes a large language model on a custom dataset with LoRA/QLoRA configuration and training parameters" },
        new() { Name = "extract_entities", Description = "Performs Named Entity Recognition (NER) on text to extract people, organizations, locations, dates, and custom entities" },
        new() { Name = "cluster_data", Description = "Performs clustering analysis on a dataset using K-means, DBSCAN, or hierarchical methods with visualization" },
    ];
}

// ============================================================================
// Scenario Definitions — 12 real-world, paragraph-length prompts
// Each scenario has a name, verbose prompt, and expected tool names.
// Used by LLMDistillationMax to benchmark Mode 1 vs Mode 2 routing.
// ============================================================================

static class Scenarios
{
    public static (string Name, string Prompt, string[] Expected)[] Build() =>
    [
        (
            "Infrastructure Chaos",
            "So yesterday I was in a meeting with the VP of Engineering and we were discussing " +
            "the quarterly infrastructure review, and she mentioned that our Kubernetes clusters " +
            "have been showing some weird behavior lately — pods restarting randomly, memory " +
            "usage spiking at odd hours. I also noticed that our main PostgreSQL database has " +
            "been running slower than usual, especially the customer analytics queries that feed " +
            "into the monthly report. While I was investigating, I found some old SQL queries " +
            "that haven't been optimized in years. Oh and speaking of databases, the security " +
            "team flagged that we need to rotate all our database credentials by end of week, " +
            "and I should probably run a full vulnerability scan on the production environment " +
            "while I'm at it. The DevOps lead also mentioned something about the Terraform state " +
            "file being out of sync, so I need to run a plan to see what's drifted.",
            ["check_service_health", "query_database", "rotate_credentials", "scan_vulnerabilities", "kubectl_apply", "terraform_plan"]
        ),
        (
            "Marketing Multi-Hat Day",
            "OK so I just got out of the most chaotic Monday morning standup ever. The marketing " +
            "director is freaking out because apparently the competitor launched a new product over " +
            "the weekend and she wants me to do like five things at once. First she needs someone " +
            "to scrape the competitor's pricing page — you know, the one that keeps changing their " +
            "product tiers every month. Then she wants a sentiment analysis run on all the social " +
            "media mentions from the last 48 hours to see how customers are reacting to the launch. " +
            "Oh and she also asked if I could put together a monitoring dashboard showing our web " +
            "traffic metrics compared to last quarter, because the board meeting is Thursday and " +
            "she needs pretty charts. And somewhere in there she mentioned wanting to send a mass " +
            "email campaign to our subscriber list but I'm not even sure we have the email list " +
            "ready, I think the CSV is on someone's shared drive somewhere and I need to find it " +
            "and import it first before we can do anything with it.",
            ["scrape_webpage", "analyze_sentiment", "create_dashboard", "get_metrics", "send_email", "search_files", "import_csv"]
        ),
        (
            "Developer Sprint Panic",
            "Right, so sprint review is in two days and I'm looking at my Jira board thinking how " +
            "did I let this happen again. I've got three PRs that need reviewing, the unit tests " +
            "for the payment module are failing on CI and nobody knows why — the pipeline just shows " +
            "red and the error logs are a mess. I need to check the pipeline status and figure out " +
            "what's going on, then probably deploy the hotfix for that authentication bug to staging " +
            "before the product owner sees it. But first I should probably check if the SSL certificates " +
            "haven't expired because we got that warning email last week and nobody followed up on it. " +
            "Also the junior dev on my team just pinged me asking for help understanding some legacy " +
            "Python code that's throwing weird errors, and I promised the PM I'd generate a progress " +
            "report by end of day showing what we've accomplished this sprint. The DevOps team also " +
            "mentioned that our Docker images are getting bloated — the base image alone is like 2GB " +
            "now — and we should probably rebuild them. I haven't even looked at the error logs from " +
            "last night's batch job failure yet, ugh.",
            ["get_pipeline_status", "deploy_to_environment", "check_ssl_certificate", "explain_code", "generate_report", "docker_build", "query_logs", "get_error_report"]
        ),
        (
            "Data Science Deep Dive",
            "I've been thinking about this all weekend and I think there's a really interesting " +
            "opportunity with our customer data that nobody's explored yet. We have something like " +
            "fifty thousand customer records with behavioral data — page views, purchase history, " +
            "support tickets, NPS scores, the whole nine yards. What if we clustered them into " +
            "meaningful segments using unsupervised learning? I bet we could find patterns that " +
            "the marketing team would absolutely kill for. But first I'd need to clean the data " +
            "and probably export it from the production database into something workable, then " +
            "maybe run some descriptive statistics to understand the distributions and identify " +
            "outliers. The tricky part is that some of the data is in different formats — some " +
            "sitting in the main database, some in CSV files that the analytics team exported " +
            "last month and dumped on the shared drive. I'd also need to generate embeddings for " +
            "the text fields like support ticket descriptions to do any meaningful NLP analysis. " +
            "And while we're at it, maybe we should train a classification model to predict churn " +
            "risk — the VP of Customer Success has been asking about that for months. Oh, and the " +
            "sentiment analysis on our NPS survey free-text responses is still pending too.",
            ["query_database", "export_to_csv", "calculate_statistics", "import_csv", "generate_embeddings", "cluster_data", "train_model", "classify_text", "analyze_sentiment"]
        ),
        (
            "Global Team Coordinator",
            "Being a remote team lead across four timezones is honestly exhausting some days, but " +
            "today takes the cake. So I need to figure out when to schedule our quarterly architecture " +
            "review meeting — there's engineers in Tokyo, London, San Francisco, and São Paulo, and " +
            "half of them have weird schedule constraints that nobody told me about until this morning. " +
            "Before I send out any calendar invites I should probably check everyone's availability " +
            "first so I don't accidentally book over someone's focus time again like last month. " +
            "Actually, I also need to translate the meeting agenda into Japanese because Tanaka-san " +
            "mentioned she strongly prefers reading technical documents in her native language, which " +
            "is totally fair. Oh and I need to send a Slack message to the London team about the API " +
            "freeze starting next week — they keep missing announcements — and then a separate Teams " +
            "message to the São Paulo office about the updated code review process that went into " +
            "effect yesterday. Come to think of it, I should also set up a recurring daily standup " +
            "that actually works across all these timezones without making anyone join at midnight, " +
            "and create a shared document with the meeting notes template so everyone's on the same page.",
            ["check_availability", "get_timezone_info", "create_calendar_event", "translate_text", "send_slack_message", "send_teams_message", "set_recurring_event", "write_file"]
        ),
        (
            "Security Incident Response",
            "OK this is NOT a drill, and I'm honestly trying not to panic right now. We just got " +
            "an automated alert that there might be unauthorized access attempts on our production " +
            "systems. The monitoring dashboard is showing a massive spike in failed authentication " +
            "attempts starting around 3am, and there are some suspicious API calls from IP addresses " +
            "that don't match any of our known offices or VPN ranges. I need to immediately check " +
            "the access logs and audit trail to understand the scope of what happened, then audit " +
            "who currently has permissions to what — because honestly I don't think our IAM policies " +
            "have been reviewed in six months. We should probably generate fresh API keys for all " +
            "the potentially compromised services and rotate the database credentials as a precaution. " +
            "The CISO wants a full incident report by 5pm with a timeline and impact assessment. " +
            "We also need to verify that our encryption at rest is actually working properly and " +
            "that the SSL certs are still valid. While we're at it, we should hash all the sensitive " +
            "customer data that's apparently sitting in near-plain-text in that legacy system nobody " +
            "wants to touch, and update the JWT token configuration to use shorter expiration windows. " +
            "And someone needs to run a comprehensive vulnerability scan on everything, yesterday.",
            ["query_logs", "audit_permissions", "generate_api_key", "rotate_credentials", "generate_report", "check_ssl_certificate", "encrypt_data", "hash_data", "generate_jwt_token", "scan_vulnerabilities"]
        ),
        (
            "End-of-Quarter Reporting",
            "It's the last day of Q3 and my boss just dropped a bomb on me — apparently the entire " +
            "executive leadership team wants a comprehensive quarterly business review report on their " +
            "desks by tomorrow morning, and somehow this is now my problem. I need to pull the revenue " +
            "data from the main analytics database, compute quarter-over-quarter growth rates, run " +
            "some statistical analysis on customer acquisition costs, and compare everything with Q2 " +
            "numbers to show trends. The CFO specifically asked for compound interest projections on " +
            "our company's investment portfolio going out 5 years, which means I need to dig up the " +
            "portfolio details from somewhere. The data is, predictably, scattered across multiple " +
            "sources: some in the database, some in Excel spreadsheets that the finance team maintains " +
            "on SharePoint, and the web traffic engagement numbers apparently need to be scraped from " +
            "our analytics tool's export page because the API is broken again. I also need to do unit " +
            "conversions for the international sales figures — they're reported in euros, pounds, and " +
            "yen but the final report needs everything normalized to USD. And then somehow I need to " +
            "make all of this look presentable with proper charts and formatted tables in a generated " +
            "report, plus export key datasets to CSV for the department leads to do their own analysis.",
            ["query_database", "calculate_statistics", "calculate_compound_interest", "import_csv", "scrape_webpage", "convert_units", "generate_report", "export_to_csv"]
        ),
        (
            "ML Pipeline Debug Session",
            "Alright, so our ML pipeline has been acting up again and I've spent the entire morning " +
            "pulling my hair out trying to figure out what went wrong. The sentiment analysis model " +
            "we deployed to production last Thursday is giving completely nonsensical predictions — " +
            "like it's classifying obviously glowing five-star reviews as negative sentiment, and " +
            "angry complaint emails as positive, which obviously makes zero sense and the customer " +
            "success team is furious. I need to run some targeted inference tests with known inputs " +
            "to verify the model outputs match expected values, then probably evaluate the full model " +
            "on our holdout test dataset to check if the accuracy metrics have degraded since we last " +
            "benchmarked it. I'm starting to suspect we need to either retrain the model from scratch " +
            "with more recent labeled data, or maybe fine-tune the base language model with our " +
            "domain-specific customer feedback corpus. The training data might be contaminated — I " +
            "should check if the CSV we used for training had any systematic labeling errors or " +
            "duplicates. Oh and the data scientist on my team wants me to run a clustering analysis " +
            "on all the misclassified examples to see if there's a pattern, like maybe the model " +
            "fails on a specific product category or customer demographic. The model runs as a Docker " +
            "container, so I should also check if the container is actually healthy and pull the " +
            "prediction logs to look for out-of-memory errors or anything obvious.",
            ["run_inference", "evaluate_model", "analyze_sentiment", "train_model", "finetune_llm", "import_csv", "cluster_data", "check_service_health", "query_logs", "docker_build"]
        ),
        (
            "New Developer Onboarding",
            "So we have three brand new engineers starting next Monday and I volunteered — well, more " +
            "like was voluntold — to have everything ready for their first day. The onboarding checklist " +
            "is honestly kind of insane. I need to send each of them a welcome email with login " +
            "credentials and links to our internal documentation, then create calendar events for all " +
            "the orientation sessions: the HR intro on Monday morning, the engineering culture talk on " +
            "Tuesday, the architecture deep-dive on Wednesday, and the first sprint planning on Thursday. " +
            "Each new hire also needs a Slack welcome message posted in the team channel introducing " +
            "them and linking to the team handbook. On the technical side, I need to generate development " +
            "API keys for each person with appropriate scoping so they can't accidentally hit production, " +
            "and properly audit and configure their system permissions — last time we onboarded someone " +
            "they accidentally got admin access to the production database, which was a whole thing. " +
            "I should probably write an updated getting-started guide since the current one still " +
            "references our old CI system that we deprecated six months ago. Oh and I need to set up " +
            "recurring weekly one-on-one meetings between each new hire and their assigned mentor, " +
            "export the current org chart from the database so they can see who's who on each team, " +
            "and make sure the development Kubernetes cluster pods are actually running properly.",
            ["send_email", "create_calendar_event", "send_slack_message", "generate_api_key", "audit_permissions", "write_file", "set_recurring_event", "export_to_csv", "kubectl_apply", "check_service_health"]
        ),
        (
            "Weather-Dependent Event",
            "My team somehow elected me to organize the annual company offsite retreat and I'm quickly " +
            "realizing this is basically a full-time job on top of my actual full-time job. The event " +
            "is planned for a coastal resort location next month, so weather is absolutely critical to " +
            "get right — I need to pull the extended forecast for that area to check if we're looking " +
            "at rain, because last year it poured and we had zero backup plans. I also need to check " +
            "the UV index because half the engineering team has never seen the sun and the other half " +
            "got second-degree sunburns last year, plus the tide schedule since we've got kayaking and " +
            "beach volleyball on the agenda. Then there's the whole communications piece — I need to " +
            "send a detailed email blast to all 200 employees with the finalized agenda, logistics " +
            "info, and packing suggestions. We should set up a dedicated Teams channel for real-time " +
            "event coordination and questions. Oh and we have employees flying in from Mexico City " +
            "and Shanghai, so I need to translate the safety briefing and event schedule into Spanish " +
            "and Mandarin Chinese. The finance department wants me to calculate the per-person cost " +
            "breakdown and convert the expenses from the resort's local currency into USD, EUR, and " +
            "GBP for the international attendees' expense reports. I should probably also create a " +
            "shared logistics document with room assignments and travel details.",
            ["get_weather_forecast", "get_uv_index", "get_tide_info", "send_email", "send_teams_message", "translate_text", "calculate_expression", "convert_units", "write_file"]
        ),
        (
            "Content Pipeline Emergency",
            "Everything is on fire today in the content team and somehow I'm the one putting out the " +
            "flames even though I'm supposed to be an engineer. The flagship blog post that was supposed " +
            "to go live at 9am this morning has a bunch of embarrassing grammar and spelling errors " +
            "that somehow made it through three rounds of review, and the social media team is sitting " +
            "there refreshing their dashboards waiting for me to green-light the publish. I need to " +
            "run a thorough grammar and spell check on the draft immediately, then also create a " +
            "condensed summary of the post for the Twitter thread and a slightly longer version for " +
            "LinkedIn. The SEO team has been on my case about keyword optimization, so I need to " +
            "extract the primary keywords and key phrases from our last five published articles to " +
            "make sure we're targeting the right search terms. While I'm digging through the content " +
            "archive, I noticed that some of our older articles have embedded URLs that might be dead " +
            "links — I should check those before Google penalizes us for it. Oh and we're expanding " +
            "into three new European markets next month, so the localization team needs translations " +
            "of all our product descriptions into French, German, and Portuguese by end of week. The " +
            "CMO also wants a sentiment analysis on brand mentions from the past month, and someone " +
            "suggested we scrape the top competitor blogs to see what content themes are trending in " +
            "our space. This is honestly too much work for one person but here we are.",
            ["check_grammar", "summarize_text", "extract_keywords", "check_url_status", "translate_text", "analyze_sentiment", "scrape_webpage"]
        ),
        (
            "Late-Night Production Outage",
            "It's 2am and I just got woken up by PagerDuty because production is apparently on fire " +
            "and the on-call engineer before me somehow slept through their alerts for three hours, " +
            "which is a whole separate conversation we need to have on Monday. Users are flooding " +
            "our support channels complaining about 500 errors, timeout pages, and lost transactions. " +
            "First thing I absolutely need to do is check the service health across all our endpoints " +
            "to see exactly what's down and what's still limping along. Then I need to pull up the " +
            "error logs and application traces to understand what actually broke — the monitoring " +
            "dashboard is showing that our p99 latency spiked from 200ms to 30 seconds right around " +
            "midnight, which happens to be exactly when the database team was supposedly running a " +
            "schema migration. I bet that migration is related, so I need to check if it completed " +
            "successfully or if it's stuck halfway through and locking tables. If the migration is " +
            "the culprit, we might need to roll back the entire deployment to the previous stable " +
            "version while we figure out what went wrong. I also need to trace a few specific failed " +
            "requests end-to-end through our microservice call chain to pinpoint where things are " +
            "breaking down. After that, I need to set up proper alerting so this kind of three-hour " +
            "silent failure never happens again. The incident commander wants a preliminary post-mortem " +
            "report drafted by morning, and I should send a status update to the engineering team via " +
            "email and Slack so nobody walks into a surprise tomorrow.",
            ["check_service_health", "get_error_report", "query_logs", "get_metrics", "run_migration", "rollback_deployment", "trace_request", "set_alert_rule", "generate_report", "send_email", "send_slack_message"]
        ),
    ];
}

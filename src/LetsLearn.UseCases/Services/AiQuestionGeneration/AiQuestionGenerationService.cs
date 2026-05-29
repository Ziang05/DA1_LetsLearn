using System.Text.Json;
using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.UseCases.DTOs;
using LetsLearn.UseCases.ServiceInterfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LetsLearn.UseCases.Services.AiQuestionGeneration
{
    public class AiQuestionGenerationService : IAiQuestionGenerationService
    {
        private readonly IUnitOfWork _uow;
        private readonly IDocumentTextExtractor _extractor;
        private readonly IDocumentChunkingService _chunkingService;
        private readonly IAiLlmClient _llmClient;
        private readonly IAiEmbeddingClient _embeddingClient;
        private readonly IAiVectorStore _vectorStore;
        private readonly IAiQuotaService _quotaService;
        private readonly IAiQuestionGenerationJobQueue _jobQueue;
        private readonly IQuestionService _questionService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiQuestionGenerationService> _logger;

        public AiQuestionGenerationService(
            IUnitOfWork uow,
            IDocumentTextExtractor extractor,
            IDocumentChunkingService chunkingService,
            IAiLlmClient llmClient,
            IAiEmbeddingClient embeddingClient,
            IAiVectorStore vectorStore,
            IAiQuotaService quotaService,
            IAiQuestionGenerationJobQueue jobQueue,
            IQuestionService questionService,
            IConfiguration configuration,
            ILogger<AiQuestionGenerationService> logger)
        {
            _uow = uow;
            _extractor = extractor;
            _chunkingService = chunkingService;
            _llmClient = llmClient;
            _embeddingClient = embeddingClient;
            _vectorStore = vectorStore;
            _quotaService = quotaService;
            _jobQueue = jobQueue;
            _questionService = questionService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<UploadLectureDocumentResponse> UploadDocumentAsync(IFormFile file, string courseId, Guid userId, bool bypassTeacherCheck = false, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("File is required.");
            }

            _logger.LogInformation(
                "[AI Questions] Upload started. CourseId={CourseId}, UserId={UserId}, FileName={FileName}, SizeBytes={SizeBytes}, ContentType={ContentType}",
                courseId,
                userId,
                file.FileName,
                file.Length,
                file.ContentType);

            await EnsureUserCanAccessCourse(courseId, userId, bypassTeacherCheck, ct);
            await EnforceQuotaAsync(userId, "uploads-per-day", 1, GetQuota("UploadsPerUserPerDay", 20), TimeSpan.FromDays(1), ct);

            var maxFileSizeMb = GetQuota("MaxUploadFileSizeMb", 25);
            if (file.Length > maxFileSizeMb * 1024L * 1024L)
            {
                throw new InvalidOperationException($"File is too large. Max upload size is {maxFileSizeMb} MB.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".docx" and not ".pdf" and not ".txt")
            {
                throw new NotSupportedException("Only .docx, .pdf, and .txt files are accepted.");
            }

            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "ai");
            Directory.CreateDirectory(uploadRoot);
            var savedFileName = $"{Guid.NewGuid():N}{extension}";
            var savedPath = Path.Combine(uploadRoot, savedFileName);

            await using (var fileStream = File.Create(savedPath))
            {
                await file.CopyToAsync(fileStream, ct);
            }

            var extracted = await _extractor.ExtractAsync(file, savedPath, ct);
            var chunkDtos = _chunkingService.Chunk(extracted);
            _logger.LogInformation(
                "[AI Questions] Document extracted and chunked. CourseId={CourseId}, UserId={UserId}, FileName={FileName}, ExtractedChars={ExtractedChars}, ChunkCount={ChunkCount}",
                courseId,
                userId,
                file.FileName,
                extracted.Text.Length,
                chunkDtos.Count);
            if (chunkDtos.Count == 0)
            {
                throw new InvalidOperationException("No readable text was found in the uploaded document.");
            }

            var maxChunksPerDocument = GetQuota("MaxChunksPerDocument", 200);
            if (chunkDtos.Count > maxChunksPerDocument)
            {
                throw new InvalidOperationException($"Document is too large after chunking. Max chunks per document is {maxChunksPerDocument}.");
            }

            await EnforceQuotaAsync(userId, "embedding-chunks-per-day", chunkDtos.Count, GetQuota("EmbeddingChunksPerUserPerDay", 1000), TimeSpan.FromDays(1), ct);

            var documentId = Guid.NewGuid();
            var document = new LectureDocument
            {
                Id = documentId,
                CourseId = courseId,
                UploadedById = userId,
                FileName = file.FileName,
                ContentType = file.ContentType ?? string.Empty,
                FilePath = savedPath,
                Status = "processed",
                CreatedAt = DateTime.UtcNow
            };

            var embeddings = await _embeddingClient.CreateEmbeddingsAsync(chunkDtos.Select(c => BuildEmbeddingInput(c.Heading, c.Content)), ct);
            var embeddingModel = _configuration["AI:EmbeddingModel"] ?? "gemini-embedding-001";
            var embeddedCount = embeddings.Count(e => e != null && e.Length > 0);
            _logger.LogInformation(
                "[AI Questions] Embeddings created. DocumentId={DocumentId}, CourseId={CourseId}, ChunkCount={ChunkCount}, EmbeddedCount={EmbeddedCount}, EmbeddingModel={EmbeddingModel}",
                documentId,
                courseId,
                chunkDtos.Count,
                embeddedCount,
                embeddingModel);

            var chunks = chunkDtos.Select((c, index) => new LectureChunk
            {
                Id = c.Id,
                DocumentId = documentId,
                CourseId = courseId,
                Heading = c.Heading,
                Content = c.Content,
                EmbeddingJson = index < embeddings.Count && embeddings[index] != null
                    ? JsonSerializer.Serialize(embeddings[index])
                    : null,
                EmbeddingModel = index < embeddings.Count && embeddings[index] != null
                    ? embeddingModel
                    : null,
                ChunkOrder = c.ChunkOrder,
                PageNumber = c.PageNumber,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _uow.LectureDocuments.AddAsync(document);
            await _uow.LectureChunks.AddRangeAsync(chunks);
            await _uow.CommitAsync();

            await _vectorStore.UpsertChunkEmbeddingsAsync(chunks
                .Select(c => new LectureChunkVector
                {
                    ChunkId = c.Id,
                    Embedding = Deserialize<float[]>(c.EmbeddingJson) ?? Array.Empty<float>()
                }), ct);

            _logger.LogInformation(
                "[AI Questions] Upload completed. DocumentId={DocumentId}, CourseId={CourseId}, ChunkCount={ChunkCount}, EmbeddedCount={EmbeddedCount}",
                documentId,
                courseId,
                chunks.Count,
                embeddedCount);

            return new UploadLectureDocumentResponse
            {
                DocumentId = documentId,
                CourseId = courseId,
                FileName = file.FileName,
                ChunkCount = chunks.Count,
                Status = document.Status
            };
        }

        public async Task<GenerateAiQuestionsResponse> GenerateQuestionsAsync(GenerateAiQuestionsRequest request, Guid userId, bool bypassTeacherCheck = false, CancellationToken ct = default)
        {
            if (request.QuestionCount is < 1 or > 50)
            {
                throw new ArgumentOutOfRangeException(nameof(request.QuestionCount), "QuestionCount must be between 1 and 50.");
            }

            await EnsureUserCanAccessCourse(request.CourseId, userId, bypassTeacherCheck, ct);
            await EnforceQuotaAsync(userId, "generate-requests-per-minute", 1, GetQuota("GenerateRequestsPerUserPerMinute", 5), TimeSpan.FromMinutes(1), ct);
            await EnforceQuotaAsync(userId, "generations-per-day", 1, GetQuota("GenerationsPerUserPerDay", 50), TimeSpan.FromDays(1), ct);
            await EnforceQuotaAsync(userId, "generated-questions-per-day", request.QuestionCount, GetQuota("GeneratedQuestionsPerUserPerDay", 200), TimeSpan.FromDays(1), ct);

            var document = await _uow.LectureDocuments.GetByIdAsync(request.DocumentId, ct)
                ?? throw new KeyNotFoundException("Lecture document not found.");
            if (document.CourseId != request.CourseId)
            {
                throw new InvalidOperationException("Lecture document does not belong to this course.");
            }

            var job = new AiQuestionGenerationJob
            {
                Id = Guid.NewGuid(),
                DocumentId = request.DocumentId,
                CourseId = request.CourseId,
                CreatedById = userId,
                BloomLevel = NormalizeBloomLevel(request.BloomLevel),
                QuestionType = NormalizeQuestionType(request.QuestionType),
                QuestionCount = request.QuestionCount,
                RetrievalTopK = Math.Clamp(request.TopK, 1, 12),
                KnowledgePoint = NormalizeKnowledgePoint(request.KnowledgePoint),
                Status = "queued",
                CreatedAt = DateTime.UtcNow
            };

            await _uow.AiQuestionGenerationJobs.AddAsync(job);
            await _uow.CommitAsync();
            await _jobQueue.EnqueueAsync(job.Id, ct);

            _logger.LogInformation(
                "[AI Questions] Generation job queued. JobId={JobId}, DocumentId={DocumentId}, CourseId={CourseId}, UserId={UserId}, BloomLevel={BloomLevel}, QuestionType={QuestionType}, QuestionCount={QuestionCount}, TopK={TopK}, HasKnowledgePoint={HasKnowledgePoint}",
                job.Id,
                job.DocumentId,
                job.CourseId,
                userId,
                job.BloomLevel,
                job.QuestionType,
                job.QuestionCount,
                job.RetrievalTopK,
                !string.IsNullOrWhiteSpace(job.KnowledgePoint));

            return new GenerateAiQuestionsResponse
            {
                JobId = job.Id,
                Status = job.Status,
                ErrorMessage = job.ErrorMessage,
                KnowledgePoint = job.KnowledgePoint,
                Questions = new List<AiGeneratedQuestionResponse>()
            };
        }

        public async Task ProcessGenerationJobAsync(Guid jobId, CancellationToken ct = default)
        {
            var job = await _uow.AiQuestionGenerationJobs.GetByIdAsync(jobId, ct)
                ?? throw new KeyNotFoundException("AI generation job not found.");

            if (job.Status is "completed" or "needs_review" or "failed")
            {
                _logger.LogInformation("[AI Questions] Skipping job {JobId} because status is {Status}.", job.Id, job.Status);
                return;
            }

            job.Status = "running";
            job.ErrorMessage = null;
            await _uow.CommitAsync();

            _logger.LogInformation(
                "[AI Questions] Job started. JobId={JobId}, DocumentId={DocumentId}, CourseId={CourseId}, BloomLevel={BloomLevel}, QuestionCount={QuestionCount}, TopK={TopK}, HasKnowledgePoint={HasKnowledgePoint}",
                job.Id,
                job.DocumentId,
                job.CourseId,
                job.BloomLevel,
                job.QuestionCount,
                job.RetrievalTopK,
                !string.IsNullOrWhiteSpace(job.KnowledgePoint));

            try
            {
                var chunks = (await _uow.LectureChunks.FindAsync(c => c.DocumentId == job.DocumentId, ct))
                    .Where(c => c != null)
                    .Cast<LectureChunk>()
                    .OrderBy(c => c.ChunkOrder)
                    .ToList();

                var selectedChunks = await RetrieveChunksAsync(chunks, job.BloomLevel, job.RetrievalTopK, job.KnowledgePoint, ct);
                var generated = await GenerateAndEvaluateAsync(job, selectedChunks, job.CreatedById, ct);

                job.Status = generated.Any(q => q.Status == "passed") ? "completed" : "needs_review";
                job.CompletedAt = DateTime.UtcNow;
                await _uow.CommitAsync();

                _logger.LogInformation(
                    "[AI Questions] Job finished. JobId={JobId}, Status={Status}, SourceChunks={SourceChunks}, RetrievedChunks={RetrievedChunks}, GeneratedCount={GeneratedCount}, PassedCount={PassedCount}",
                    job.Id,
                    job.Status,
                    chunks.Count,
                    selectedChunks.Count,
                    generated.Count,
                    generated.Count(q => q.Status == "passed"));
            }
            catch (Exception ex)
            {
                job.Status = "failed";
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await _uow.CommitAsync();
                _logger.LogError(ex, "[AI Questions] Job failed. JobId={JobId}, DocumentId={DocumentId}, CourseId={CourseId}", job.Id, job.DocumentId, job.CourseId);
                throw;
            }
        }

        public async Task<GenerateAiQuestionsResponse> GetJobAsync(Guid jobId, Guid userId, CancellationToken ct = default)
        {
            var job = await _uow.AiQuestionGenerationJobs.GetByIdAsync(jobId, ct)
                ?? throw new KeyNotFoundException("AI generation job not found.");
            await EnsureTeacherCanAccessCourse(job.CourseId, userId, ct);

            var questions = await GetGeneratedQuestionsAsync(jobId, userId, ct);
            if (job.Status == "failed")
            {
                _logger.LogWarning(
                    "[AI Questions] Returning failed job. JobId={JobId}, DocumentId={DocumentId}, CourseId={CourseId}, ErrorMessage={ErrorMessage}",
                    job.Id,
                    job.DocumentId,
                    job.CourseId,
                    job.ErrorMessage);
            }

            return new GenerateAiQuestionsResponse
            {
                JobId = job.Id,
                Status = job.Status,
                ErrorMessage = job.ErrorMessage,
                KnowledgePoint = job.KnowledgePoint,
                Questions = questions
            };
        }

        public async Task<List<AiGeneratedQuestionResponse>> GetGeneratedQuestionsAsync(Guid jobId, Guid userId, CancellationToken ct = default)
        {
            var job = await _uow.AiQuestionGenerationJobs.GetByIdAsync(jobId, ct)
                ?? throw new KeyNotFoundException("AI generation job not found.");
            await EnsureTeacherCanAccessCourse(job.CourseId, userId, ct);

            var questions = (await _uow.AiGeneratedQuestions.FindAsync(q => q.JobId == jobId, ct))
                .Where(q => q != null)
                .Cast<AiGeneratedQuestion>()
                .OrderByDescending(q => q.Score)
                .ThenBy(q => q.QuestionName)
                .ToList();

            return questions.Select(MapGeneratedQuestion).ToList();
        }

        public async Task<ApproveGeneratedQuestionsResponse> ApproveQuestionsAsync(ApproveGeneratedQuestionsRequest request, Guid userId, CancellationToken ct = default)
        {
            if (request.GeneratedQuestionIds.Count == 0)
            {
                throw new ArgumentException("At least one generated question id is required.");
            }

            var requests = new List<CreateQuestionRequest>();
            var approved = new List<AiGeneratedQuestion>();

            foreach (var id in request.GeneratedQuestionIds.Distinct())
            {
                var generated = await _uow.AiGeneratedQuestions.GetByIdAsync(id, ct)
                    ?? throw new KeyNotFoundException($"Generated question {id} was not found.");
                await EnsureTeacherCanAccessCourse(generated.CourseId, userId, ct);

                var choices = Deserialize<List<AiGeneratedChoiceDto>>(generated.ChoicesJson) ?? new List<AiGeneratedChoiceDto>();
                requests.Add(new CreateQuestionRequest
                {
                    CourseId = generated.CourseId,
                    QuestionName = generated.QuestionName,
                    QuestionText = generated.QuestionText,
                    Type = generated.Type == "MultipleChoice" ? "Choices Answer" : generated.Type,
                    Status = "active",
                    DefaultMark = 1,
                    Multiple = generated.Type is "MultipleChoice" or "Choices Answer",
                    Choices = choices.Select(c => new CreateQuestionChoiceRequest
                    {
                        Text = c.Text,
                        GradePercent = c.GradePercent,
                        Feedback = c.Feedback
                    }).ToList()
                });

                generated.Status = "approved";
                approved.Add(generated);
            }

            var savedCount = await _questionService.BulkCreateAsync(requests, userId, ct);
            foreach (var generated in approved)
            {
                generated.Status = "saved_to_bank";
            }

            await _uow.CommitAsync();
            _logger.LogInformation(
                "[AI Questions] Approved generated questions. UserId={UserId}, RequestedCount={RequestedCount}, SavedCount={SavedCount}",
                userId,
                request.GeneratedQuestionIds.Count,
                savedCount);
            return new ApproveGeneratedQuestionsResponse { SavedCount = savedCount };
        }

        private async Task<List<AiGeneratedQuestion>> GenerateAndEvaluateAsync(
            AiQuestionGenerationJob job,
            List<LectureChunk> chunks,
            Guid userId,
            CancellationToken ct)
        {
            var maxAttempts = int.TryParse(_configuration["AI:MaxRetry"], out var configuredMaxRetry) ? configuredMaxRetry : 3;
            var threshold = decimal.TryParse(_configuration["AI:PassThreshold"], out var configuredThreshold) ? configuredThreshold : 0.7m;
            var accepted = new List<AiGeneratedQuestion>();
            var bestAttempts = new Dictionary<int, AiGeneratedQuestion>();
            var feedback = string.Empty;

            for (var attempt = 1; attempt <= maxAttempts && accepted.Count < job.QuestionCount; attempt++)
            {
                var countToGenerate = job.QuestionCount - accepted.Count;
                _logger.LogInformation(
                    "[AI Questions] Generation attempt started. JobId={JobId}, Attempt={Attempt}, RequestedCount={RequestedCount}, AcceptedSoFar={AcceptedCount}",
                    job.Id,
                    attempt,
                    countToGenerate,
                    accepted.Count);
                var drafts = await GenerateDraftsAsync(job, chunks, countToGenerate, attempt, feedback, ct);

                for (var index = 0; index < drafts.Count && accepted.Count < job.QuestionCount; index++)
                {
                    var draft = drafts[index];
                    var evaluation = await EvaluateDraftAsync(draft, job.BloomLevel, chunks, ct);
                    var generated = CreateGeneratedQuestion(job, draft, evaluation, attempt, userId);

                    await _uow.AiGeneratedQuestions.AddAsync(generated);
                    await _uow.AiEvaluationResults.AddAsync(new AiEvaluationResult
                    {
                        Id = Guid.NewGuid(),
                        GeneratedQuestionId = generated.Id,
                        BloomAlignment = evaluation.BloomAlignment,
                        Grounding = evaluation.Grounding,
                        Clarity = evaluation.Clarity,
                        DistractorQuality = evaluation.DistractorQuality,
                        FinalScore = evaluation.FinalScore,
                        ReasonsJson = JsonSerializer.Serialize(evaluation.Reasons),
                        CreatedAt = DateTime.UtcNow
                    });

                    var slot = accepted.Count + index;
                    if (!bestAttempts.TryGetValue(slot, out var best) || generated.Score > best.Score)
                    {
                        bestAttempts[slot] = generated;
                    }

                    if (generated.Score >= threshold)
                    {
                        generated.Status = "passed";
                        accepted.Add(generated);
                    }

                    _logger.LogInformation(
                        "[AI Questions] Draft evaluated. JobId={JobId}, Attempt={Attempt}, GeneratedQuestionId={GeneratedQuestionId}, Score={Score}, Status={Status}, Bloom={Bloom}, Grounding={Grounding}, Clarity={Clarity}, Distractor={Distractor}",
                        job.Id,
                        attempt,
                        generated.Id,
                        generated.Score,
                        generated.Status,
                        evaluation.BloomAlignment,
                        evaluation.Grounding,
                        evaluation.Clarity,
                        evaluation.DistractorQuality);
                }

                _logger.LogInformation(
                    "[AI Questions] Generation attempt finished. JobId={JobId}, Attempt={Attempt}, ParsedDrafts={ParsedDrafts}, AcceptedCount={AcceptedCount}, BestAttemptCount={BestAttemptCount}",
                    job.Id,
                    attempt,
                    drafts.Count,
                    accepted.Count,
                    bestAttempts.Count);
                feedback = BuildFeedback(bestAttempts.Values.ToList());
            }

            if (accepted.Count < job.QuestionCount)
            {
                foreach (var best in bestAttempts.Values.OrderByDescending(q => q.Score))
                {
                    if (accepted.Count >= job.QuestionCount)
                    {
                        break;
                    }

                    if (!accepted.Any(q => q.Id == best.Id))
                    {
                        best.Status = "needs_teacher_review";
                        accepted.Add(best);
                    }
                }
            }

            await _uow.CommitAsync();
            return accepted;
        }

        private async Task<List<GeneratedQuestionDraft>> GenerateDraftsAsync(
            AiQuestionGenerationJob job,
            List<LectureChunk> chunks,
            int count,
            int attempt,
            string feedback,
            CancellationToken ct)
        {
            var systemPrompt = BuildGeneratorSystemPrompt();
            var userPrompt = BuildGeneratorUserPrompt(job, chunks, count, feedback);
            var json = await _llmClient.GenerateJsonAsync(systemPrompt, userPrompt, 0.7m, ct);
            var parsed = ParseDrafts(json, job, chunks);

            if (parsed.Count > 0)
            {
                _logger.LogInformation(
                    "[AI Questions] Generator returned valid drafts. JobId={JobId}, Attempt={Attempt}, RequestedCount={RequestedCount}, ParsedCount={ParsedCount}",
                    job.Id,
                    attempt,
                    count,
                    parsed.Count);
                return parsed.Take(count).ToList();
            }

            _logger.LogWarning(
                "[AI Questions] Generator returned no valid drafts; using fallback. JobId={JobId}, Attempt={Attempt}, RequestedCount={RequestedCount}, RawResponseChars={RawResponseChars}",
                job.Id,
                attempt,
                count,
                json?.Length ?? 0);
            return BuildFallbackDrafts(job, chunks, count, attempt);
        }

        private static string BuildGeneratorSystemPrompt()
        {
            return """
                You generate high-quality learning assessment questions from trusted course context only.
                Return only valid JSON. Do not use markdown fences. Do not add commentary outside JSON.
                Every question must be answerable from the provided chunks and must cite at least one chunk id in groundingRefs.
                Do not invent facts, names, numbers, formulas, or definitions that are absent from the chunks.
                Match the requested Bloom level exactly.

                Required JSON schema:
                {
                  "questions": [
                    {
                      "questionName": "short stable title",
                      "questionText": "clear question ending with ?",
                      "type": "MultipleChoice",
                      "bloomLevel": "Remember|Understand|Apply|Analyze|Evaluate|Create",
                      "choices": [
                        { "text": "correct answer", "gradePercent": 100, "feedback": "why this is correct" },
                        { "text": "plausible distractor", "gradePercent": 0, "feedback": "why this is wrong" },
                        { "text": "plausible distractor", "gradePercent": 0, "feedback": "why this is wrong" },
                        { "text": "plausible distractor", "gradePercent": 0, "feedback": "why this is wrong" }
                      ],
                      "groundingRefs": ["chunk-guid"],
                      "feedback": "short teacher-facing explanation"
                    }
                  ]
                }

                MultipleChoice rules:
                - exactly 4 choices
                - exactly 1 choice has gradePercent 100
                - exactly 3 choices have gradePercent 0
                - choices must be mutually exclusive and similar in length
                - no "all of the above" or "none of the above"
                """;
        }

        private static string BuildGeneratorUserPrompt(AiQuestionGenerationJob job, List<LectureChunk> chunks, int count, string feedback)
        {
            var context = string.Join(Environment.NewLine + Environment.NewLine, chunks.Select(c =>
                $"[chunk:{c.Id}] heading:{c.Heading ?? "N/A"}{Environment.NewLine}{c.Content}"));

            return $"""
                Bloom={job.BloomLevel}
                N={count}
                type={job.QuestionType}
                Target knowledge point={RenderKnowledgePoint(job.KnowledgePoint)}
                Previous evaluator feedback={feedback}

                Bloom intent guide:
                Remember: recall facts, terms, definitions; use cues like list, recognize, recall, identify.
                Understand: explain meaning, summarize, classify, clarify, predict.
                Apply: use concepts in a concrete situation; respond, provide, carry out, use.
                Analyze: compare, infer relationships, causes, structure; select, differentiate, integrate, deconstruct.
                Evaluate: judge using criteria and evidence from context; check, determine, judge, reflect.
                Create: propose or construct a solution from context; generate, assemble, design, create.

                Generate exactly {count} question(s). Use only chunk ids listed below in groundingRefs.
                If a target knowledge point is provided, every question must focus on that knowledge point and must still be grounded in the chunks.

                Context chunks:
                {context}
                """;
        }

        private static List<GeneratedQuestionDraft> ParseDrafts(string? json, AiQuestionGenerationJob job, List<LectureChunk> chunks)
        {
            var payload = ExtractJsonPayload(json);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new List<GeneratedQuestionDraft>();
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var document = JsonDocument.Parse(payload);
                var normalizedJson = document.RootElement.ValueKind == JsonValueKind.Array
                    ? $"{{\"questions\":{payload}}}"
                    : payload;
                var wrapper = JsonSerializer.Deserialize<GeneratedQuestionWrapper>(normalizedJson, options);
                return wrapper?.Questions?
                    .Select(q => NormalizeDraft(q, job))
                    .Where(q => IsValidDraft(q, job, chunks))
                    .Take(job.QuestionCount)
                    .ToList()
                    ?? new List<GeneratedQuestionDraft>();
            }
            catch
            {
                return new List<GeneratedQuestionDraft>();
            }
        }

        private static GeneratedQuestionDraft NormalizeDraft(GeneratedQuestionDraft draft, AiQuestionGenerationJob job)
        {
            draft.QuestionName = string.IsNullOrWhiteSpace(draft.QuestionName)
                ? $"AI_{job.BloomLevel}_{Guid.NewGuid().ToString("N")[..8]}"
                : draft.QuestionName.Trim();
            draft.QuestionText = draft.QuestionText?.Trim() ?? string.Empty;
            draft.Type = NormalizeQuestionType(draft.Type);
            draft.BloomLevel = NormalizeBloomLevel(draft.BloomLevel);
            draft.GroundingRefs = draft.GroundingRefs?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            draft.Choices = draft.Choices?
                .Where(c => !string.IsNullOrWhiteSpace(c.Text))
                .Select(c => new AiGeneratedChoiceDto
                {
                    Text = c.Text?.Trim(),
                    GradePercent = c.GradePercent,
                    Feedback = c.Feedback?.Trim()
                })
                .ToList() ?? new List<AiGeneratedChoiceDto>();
            draft.Feedback = draft.Feedback?.Trim();
            return draft;
        }

        private static bool IsValidDraft(GeneratedQuestionDraft draft, AiQuestionGenerationJob job, List<LectureChunk> chunks)
        {
            if (string.IsNullOrWhiteSpace(draft.QuestionText) || draft.QuestionText.Length < 12)
            {
                return false;
            }

            if (!string.Equals(draft.BloomLevel, job.BloomLevel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var validChunkIds = chunks.Select(c => c.Id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (draft.GroundingRefs.Count == 0 || draft.GroundingRefs.Any(id => !validChunkIds.Contains(id)))
            {
                return false;
            }

            if (draft.Type is "MultipleChoice" or "Choices Answer")
            {
                if (draft.Choices.Count != 4)
                {
                    return false;
                }

                if (draft.Choices.Count(c => c.GradePercent == 100) != 1 || draft.Choices.Count(c => c.GradePercent == 0) != 3)
                {
                    return false;
                }

                var distinctChoiceTexts = draft.Choices
                    .Select(c => c.Text?.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                if (distinctChoiceTexts != 4)
                {
                    return false;
                }

                if (draft.Choices.Any(c => ContainsForbiddenChoiceText(c.Text)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsForbiddenChoiceText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var normalized = text.Trim().ToLowerInvariant();
            return normalized.Contains("all of the above")
                || normalized.Contains("none of the above")
                || normalized.Contains("tat ca cac dap an")
                || normalized.Contains("khong co dap an nao");
        }

        private static string? ExtractJsonPayload(string? response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            var trimmed = response.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewLine = trimmed.IndexOf('\n');
                var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                {
                    trimmed = trimmed[(firstNewLine + 1)..lastFence].Trim();
                }
            }

            if (IsJsonContainer(trimmed))
            {
                return trimmed;
            }

            var firstObject = trimmed.IndexOf('{');
            var firstArray = trimmed.IndexOf('[');
            var start = firstObject < 0
                ? firstArray
                : firstArray < 0
                    ? firstObject
                    : Math.Min(firstObject, firstArray);
            if (start < 0)
            {
                return null;
            }

            var end = trimmed[start] == '{'
                ? trimmed.LastIndexOf('}')
                : trimmed.LastIndexOf(']');
            if (end <= start)
            {
                return null;
            }

            var candidate = trimmed[start..(end + 1)].Trim();
            return IsJsonContainer(candidate) ? candidate : null;
        }

        private static bool IsJsonContainer(string value)
        {
            return (value.StartsWith('{') && value.EndsWith('}'))
                || (value.StartsWith('[') && value.EndsWith(']'));
        }

        private static List<GeneratedQuestionDraft> BuildFallbackDrafts(AiQuestionGenerationJob job, List<LectureChunk> chunks, int count, int attempt)
        {
            var sourceChunks = chunks.Count == 0 ? new List<LectureChunk>() : chunks;
            var drafts = new List<GeneratedQuestionDraft>();

            for (var i = 0; i < count; i++)
            {
                var chunk = sourceChunks.Count == 0 ? null : sourceChunks[i % sourceChunks.Count];
                var topic = chunk?.Heading ?? "nội dung bài giảng";
                var excerpt = TrimToSentence(chunk?.Content ?? "Không có đủ nội dung để tạo câu hỏi.");
                drafts.Add(new GeneratedQuestionDraft
                {
                    QuestionName = $"AI_{job.BloomLevel}_{attempt}_{i + 1}",
                    QuestionText = BuildBloomQuestion(job.BloomLevel, topic, excerpt),
                    Type = job.QuestionType,
                    BloomLevel = job.BloomLevel,
                    GroundingRefs = chunk == null ? new List<string>() : new List<string> { chunk.Id.ToString() },
                    Choices = new List<AiGeneratedChoiceDto>
                    {
                        new() { Text = excerpt, GradePercent = 100, Feedback = "Đáp án bám theo nội dung tài liệu." },
                        new() { Text = "Một nhận định không được nêu trong tài liệu.", GradePercent = 0 },
                        new() { Text = "Một kết luận trái với nội dung tài liệu.", GradePercent = 0 },
                        new() { Text = "Một phương án không liên quan đến bài học.", GradePercent = 0 }
                    }
                });
            }

            return drafts;
        }

        private static string BuildBloomQuestion(string bloomLevel, string topic, string excerpt)
        {
            return bloomLevel switch
            {
                "Remember" => $"Theo tài liệu, thông tin nào mô tả đúng về {topic}?",
                "Understand" => $"Ý chính nào giải thích đúng nội dung sau về {topic}: \"{excerpt}\"?",
                "Apply" => $"Có thể áp dụng nội dung về {topic} trong tình huống học tập nào?",
                "Analyze" => $"Mối quan hệ hoặc nguyên nhân nào thể hiện rõ nhất trong nội dung về {topic}?",
                "Evaluate" => $"Nhận định nào đánh giá hợp lý nhất về nội dung {topic} dựa trên tài liệu?",
                "Create" => $"Dựa trên nội dung {topic}, phương án nào là cách xây dựng giải pháp phù hợp nhất?",
                _ => $"Câu nào phù hợp nhất với nội dung bài giảng về {topic}?"
            };
        }

        private async Task<EvaluationScore> EvaluateDraftAsync(GeneratedQuestionDraft draft, string bloomLevel, List<LectureChunk> chunks, CancellationToken ct)
        {
            var enabled = bool.TryParse(_configuration["AI:UseEvaluatorModel"], out var configuredEnabled)
                ? configuredEnabled
                : true;

            if (!enabled)
            {
                _logger.LogInformation("[AI Questions] Evaluator model disabled; using rule-based evaluator.");
                return EvaluateDraftByRules(draft, bloomLevel, chunks);
            }

            try
            {
                var json = await _llmClient.GenerateJsonAsync(
                    BuildEvaluatorSystemPrompt(),
                    BuildEvaluatorUserPrompt(draft, bloomLevel, chunks),
                    0m,
                    ct,
                    "EvaluatorModel");

                var score = ParseEvaluatorScore(json);
                if (score != null)
                {
                    _logger.LogDebug(
                        "[AI Questions] Evaluator model returned valid score. Bloom={Bloom}, Grounding={Grounding}, Clarity={Clarity}, Distractor={Distractor}, Final={FinalScore}",
                        score.BloomAlignment,
                        score.Grounding,
                        score.Clarity,
                        score.DistractorQuality,
                        score.FinalScore);
                }
                else
                {
                    _logger.LogWarning(
                        "[AI Questions] Evaluator model returned invalid JSON or missing fields; using rule-based evaluator. RawResponseChars={RawResponseChars}",
                        json?.Length ?? 0);
                }

                return score ?? EvaluateDraftByRules(draft, bloomLevel, chunks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AI Questions] Evaluator model failed; using rule-based evaluator.");
                return EvaluateDraftByRules(draft, bloomLevel, chunks);
            }
        }

        private static string BuildEvaluatorSystemPrompt()
        {
            return """
                You are an assessment quality evaluator for AI-generated learning questions.
                Score the draft only from the provided source context.
                Return only valid JSON. Do not use markdown fences. Do not add commentary outside JSON.
                All four numeric score fields are required.
                Return only valid JSON with this schema:
                {
                  "bloomAlignment": 0.0,
                  "grounding": 0.0,
                  "clarity": 0.0,
                  "distractorQuality": 0.0,
                  "reasons": ["short reason"]
                }
                Scoring range is 0.0 to 1.0.
                bloomAlignment: requested Bloom level match.
                grounding: answerable from provided chunks and grounding refs.
                clarity: clear, unambiguous, age/course appropriate wording.
                distractorQuality: incorrect options are plausible and not obviously absurd; use 0.8 for non-MCQ when otherwise acceptable.
                """;
        }

        private static string BuildEvaluatorUserPrompt(GeneratedQuestionDraft draft, string bloomLevel, List<LectureChunk> chunks)
        {
            var context = string.Join(Environment.NewLine + Environment.NewLine, chunks.Select(c =>
                $"[chunk:{c.Id}] heading:{c.Heading ?? "N/A"}{Environment.NewLine}{c.Content}"));
            var draftJson = JsonSerializer.Serialize(draft);

            return $"""
                Requested Bloom level: {bloomLevel}

                Source context:
                {context}

                Draft question JSON:
                {draftJson}
                """;
        }

        private static EvaluationScore? ParseEvaluatorScore(string? json)
        {
            var payload = ExtractJsonPayload(json);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<EvaluatorModelResponse>(
                    payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed == null || !parsed.HasAllScores())
                {
                    return null;
                }

                var bloom = ClampScore(parsed.BloomAlignment.GetValueOrDefault());
                var grounding = ClampScore(parsed.Grounding.GetValueOrDefault());
                var clarity = ClampScore(parsed.Clarity.GetValueOrDefault());
                var distractor = ClampScore(parsed.DistractorQuality.GetValueOrDefault());
                var final = 0.4m * bloom + 0.3m * grounding + 0.2m * clarity + 0.1m * distractor;
                var reasons = parsed.Reasons?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? new List<string>();
                if (reasons.Count == 0 && final < 0.7m)
                {
                    reasons.Add("Evaluator returned a low score without detailed reasons.");
                }

                return new EvaluationScore(bloom, grounding, clarity, distractor, final, reasons);
            }
            catch
            {
                return null;
            }
        }

        private static decimal ClampScore(decimal score)
        {
            if (score < 0m) return 0m;
            if (score > 1m) return 1m;
            return score;
        }

        private static EvaluationScore EvaluateDraftByRules(GeneratedQuestionDraft draft, string bloomLevel, List<LectureChunk> chunks)
        {
            var reasons = new List<string>();
            var bloom = string.Equals(draft.BloomLevel, bloomLevel, StringComparison.OrdinalIgnoreCase) ? 1m : 0.6m;
            if (bloom < 1m) reasons.Add("Bloom level does not exactly match the request.");

            var grounding = draft.GroundingRefs.Any(id => chunks.Any(c => c.Id.ToString() == id)) ? 1m : 0.4m;
            if (grounding < 1m) reasons.Add("Grounding references are missing or invalid.");

            var clarity = draft.QuestionText.Length >= 20 && draft.QuestionText.EndsWith("?") ? 1m : 0.7m;
            if (clarity < 1m) reasons.Add("Question wording should be clearer.");

            var distractor = draft.Type is "MultipleChoice" or "Choices Answer"
                ? draft.Choices.Count >= 4 && draft.Choices.Count(c => c.GradePercent == 0) >= 3 ? 1m : 0.5m
                : 0.8m;
            if (distractor < 1m) reasons.Add("Distractors are incomplete or weak.");

            var final = 0.4m * bloom + 0.3m * grounding + 0.2m * clarity + 0.1m * distractor;
            return new EvaluationScore(bloom, grounding, clarity, distractor, final, reasons);
        }

        private static AiGeneratedQuestion CreateGeneratedQuestion(
            AiQuestionGenerationJob job,
            GeneratedQuestionDraft draft,
            EvaluationScore evaluation,
            int attempt,
            Guid userId)
        {
            return new AiGeneratedQuestion
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                CourseId = job.CourseId,
                QuestionName = string.IsNullOrWhiteSpace(draft.QuestionName)
                    ? $"AI_{job.BloomLevel}_{Guid.NewGuid().ToString("N")[..8]}"
                    : draft.QuestionName,
                QuestionText = draft.QuestionText,
                Type = NormalizeQuestionType(draft.Type),
                BloomLevel = NormalizeBloomLevel(draft.BloomLevel),
                Status = "draft",
                Score = evaluation.FinalScore,
                GroundingRefsJson = JsonSerializer.Serialize(draft.GroundingRefs),
                ChoicesJson = JsonSerializer.Serialize(draft.Choices),
                Feedback = evaluation.Reasons.Count == 0 ? draft.Feedback : string.Join(" ", evaluation.Reasons),
                Attempt = attempt,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task<List<LectureChunk>> RetrieveChunksAsync(List<LectureChunk> chunks, string bloomLevel, int topK, string? knowledgePoint, CancellationToken ct)
        {
            if (chunks.Count == 0)
            {
                return new List<LectureChunk>();
            }

            var query = BuildRetrievalQuery(bloomLevel, knowledgePoint);
            float[]? queryEmbedding = null;
            try
            {
                queryEmbedding = (await _embeddingClient.CreateEmbeddingsAsync(new[] { query }, ct)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[AI Questions] Retrieval query embedding failed; using keyword fallback. DocumentId={DocumentId}, BloomLevel={BloomLevel}, TopK={TopK}, HasKnowledgePoint={HasKnowledgePoint}",
                    chunks.First().DocumentId,
                    bloomLevel,
                    topK,
                    !string.IsNullOrWhiteSpace(knowledgePoint));
            }

            if (queryEmbedding == null || chunks.All(c => string.IsNullOrWhiteSpace(c.EmbeddingJson)))
            {
                _logger.LogInformation(
                    "[AI Questions] Retrieval using keyword fallback. DocumentId={DocumentId}, BloomLevel={BloomLevel}, ChunkCount={ChunkCount}, TopK={TopK}, HasQueryEmbedding={HasQueryEmbedding}, HasKnowledgePoint={HasKnowledgePoint}",
                    chunks.First().DocumentId,
                    bloomLevel,
                    chunks.Count,
                    topK,
                    queryEmbedding != null,
                    !string.IsNullOrWhiteSpace(knowledgePoint));
                return RetrieveChunksByKeywords(chunks, bloomLevel, topK, knowledgePoint);
            }

            var vectorChunkIds = await _vectorStore.SearchSimilarChunksAsync(chunks.First().DocumentId, queryEmbedding, topK, ct);
            if (vectorChunkIds.Count > 0)
            {
                var chunksById = chunks.ToDictionary(c => c.Id, c => c);
                var retrieved = vectorChunkIds
                    .Where(chunksById.ContainsKey)
                    .Select(id => chunksById[id])
                    .ToList();
                _logger.LogInformation(
                    "[AI Questions] Retrieval using pgvector. DocumentId={DocumentId}, BloomLevel={BloomLevel}, TopK={TopK}, RetrievedCount={RetrievedCount}, HasKnowledgePoint={HasKnowledgePoint}",
                    chunks.First().DocumentId,
                    bloomLevel,
                    topK,
                    retrieved.Count,
                    !string.IsNullOrWhiteSpace(knowledgePoint));
                return retrieved;
            }

            var terms = BuildKeywordTerms(bloomLevel, knowledgePoint);
            var fallback = chunks
                .Select(c => new
                {
                    Chunk = c,
                    VectorScore = CosineSimilarity(queryEmbedding, Deserialize<float[]>(c.EmbeddingJson)),
                    KeywordScore = terms.Count(term => c.Content.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (c.Heading?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .OrderByDescending(x => x.VectorScore)
                .ThenByDescending(x => x.KeywordScore)
                .ThenBy(x => x.Chunk.ChunkOrder)
                .Take(Math.Clamp(topK, 1, 12))
                .Select(x => x.Chunk)
                .ToList();
            _logger.LogInformation(
                "[AI Questions] Retrieval using local vector fallback. DocumentId={DocumentId}, BloomLevel={BloomLevel}, TopK={TopK}, RetrievedCount={RetrievedCount}, HasKnowledgePoint={HasKnowledgePoint}",
                chunks.First().DocumentId,
                bloomLevel,
                topK,
                fallback.Count,
                !string.IsNullOrWhiteSpace(knowledgePoint));
            return fallback;
        }

        private static List<LectureChunk> RetrieveChunksByKeywords(List<LectureChunk> chunks, string bloomLevel, int topK, string? knowledgePoint)
        {
            var terms = BuildKeywordTerms(bloomLevel, knowledgePoint);
            return chunks
                .Select(c => new
                {
                    Chunk = c,
                    Score = terms.Count(term => c.Content.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (c.Heading?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Chunk.ChunkOrder)
                .Take(Math.Clamp(topK, 1, 12))
                .Select(x => x.Chunk)
                .ToList();
        }

        private static string BuildEmbeddingInput(string? heading, string content)
        {
            return string.IsNullOrWhiteSpace(heading)
                ? content
                : $"{heading}\n{content}";
        }

        private static string BuildRetrievalQuery(string bloomLevel, string? knowledgePoint)
        {
            var bloomQuery = BuildBloomRetrievalQuery(bloomLevel);
            return string.IsNullOrWhiteSpace(knowledgePoint)
                ? bloomQuery
                : $"{knowledgePoint.Trim()}. {bloomQuery}";
        }

        private static string BuildBloomRetrievalQuery(string bloomLevel)
        {
            return bloomLevel switch
            {
                "Remember" => "facts definitions terms concepts key information list recognize recall identify from the lecture",
                "Understand" => "explanations meanings examples summaries summarize classify clarify predict of the lecture content",
                "Apply" => "procedures applications practice situations examples respond response provide carry out use lecture concepts",
                "Analyze" => "relationships causes comparisons structure patterns select differentiate integrate deconstruct in the lecture content",
                "Evaluate" => "criteria judgments advantages limitations evidence check determine judge reflect based evaluation",
                "Create" => "design propose construct solution plan generate assemble create based on lecture concepts",
                _ => "relevant lecture content for generating assessment questions"
            };
        }

        private static List<string> BuildKeywordTerms(string bloomLevel, string? knowledgePoint)
        {
            var terms = BloomTerms(bloomLevel);
            if (string.IsNullOrWhiteSpace(knowledgePoint))
            {
                return terms;
            }

            var knowledgeTerms = knowledgePoint
                .Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '/', '\\', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length >= 3)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            terms.AddRange(knowledgeTerms);
            return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static double CosineSimilarity(float[] query, float[]? chunk)
        {
            if (chunk == null || query.Length == 0 || chunk.Length == 0 || query.Length != chunk.Length)
            {
                return 0;
            }

            double dot = 0;
            double queryNorm = 0;
            double chunkNorm = 0;

            for (var i = 0; i < query.Length; i++)
            {
                dot += query[i] * chunk[i];
                queryNorm += query[i] * query[i];
                chunkNorm += chunk[i] * chunk[i];
            }

            return queryNorm == 0 || chunkNorm == 0
                ? 0
                : dot / (Math.Sqrt(queryNorm) * Math.Sqrt(chunkNorm));
        }

        private async Task EnsureTeacherCanAccessCourse(string courseId, Guid userId, CancellationToken ct)
        {
            await EnsureUserCanAccessCourse(courseId, userId, false, ct);
        }

        private async Task EnsureUserCanAccessCourse(string courseId, Guid userId, bool bypassTeacherCheck, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                throw new ArgumentException("CourseId is required.");
            }

            var course = await _uow.Course.GetByIdAsync(courseId, ct)
                ?? throw new KeyNotFoundException("Course not found.");

            if (course.CreatorId == userId)
            {
                return;
            }

            if (bypassTeacherCheck)
            {
                var enrollment = await _uow.Enrollments.GetByIdsAsync(userId, courseId, ct);
                if (enrollment != null)
                {
                    return;
                }
            }

            throw new UnauthorizedAccessException("Only the course creator or enrolled students can use AI question generation for this course.");
        }

        private async Task EnforceQuotaAsync(Guid userId, string metric, int amount, int limit, TimeSpan window, CancellationToken ct)
        {
            var enabled = bool.TryParse(_configuration["AIQuota:Enabled"], out var configuredEnabled)
                ? configuredEnabled
                : true;
            if (!enabled)
            {
                return;
            }

            await _quotaService.CheckAndConsumeAsync(userId, metric, amount, limit, window, ct);
        }

        private int GetQuota(string key, int fallback)
        {
            return int.TryParse(_configuration[$"AIQuota:{key}"], out var configured)
                ? configured
                : fallback;
        }

        private static AiGeneratedQuestionResponse MapGeneratedQuestion(AiGeneratedQuestion q)
        {
            return new AiGeneratedQuestionResponse
            {
                Id = q.Id,
                JobId = q.JobId,
                CourseId = q.CourseId,
                QuestionName = q.QuestionName,
                QuestionText = q.QuestionText,
                Type = q.Type,
                BloomLevel = q.BloomLevel,
                Status = q.Status,
                Score = q.Score,
                Attempt = q.Attempt,
                GroundingRefs = Deserialize<List<string>>(q.GroundingRefsJson) ?? new List<string>(),
                Choices = Deserialize<List<AiGeneratedChoiceDto>>(q.ChoicesJson) ?? new List<AiGeneratedChoiceDto>(),
                Feedback = q.Feedback
            };
        }

        private static T? Deserialize<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return default;
            }
        }

        private static string NormalizeBloomLevel(string? level)
        {
            var normalized = (level ?? "Understand").Trim();
            return normalized.ToLowerInvariant() switch
            {
                "remember" or "nhớ" => "Remember",
                "understand" or "hiểu" => "Understand",
                "apply" or "áp dụng" or "ap dung" => "Apply",
                "analyze" or "analyse" or "phân tích" or "phan tich" => "Analyze",
                "evaluate" or "đánh giá" or "danh gia" => "Evaluate",
                "create" or "sáng tạo" or "sang tao" => "Create",
                _ => "Understand"
            };
        }

        private static string NormalizeQuestionType(string? type)
        {
            var normalized = (type ?? "MultipleChoice").Trim();
            return normalized.ToLowerInvariant() switch
            {
                "multiplechoice" or "multiple choice" or "choice" or "choices answer" => "MultipleChoice",
                "truefalse" or "true false" => "TrueFalse",
                "shortanswer" or "short answer" => "ShortAnswer",
                _ => "MultipleChoice"
            };
        }

        private static string? NormalizeKnowledgePoint(string? knowledgePoint)
        {
            if (string.IsNullOrWhiteSpace(knowledgePoint))
            {
                return null;
            }

            var normalized = string.Join(" ", knowledgePoint.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            return normalized.Length <= 500 ? normalized : normalized[..500];
        }

        private static string RenderKnowledgePoint(string? knowledgePoint)
        {
            return string.IsNullOrWhiteSpace(knowledgePoint) ? "N/A" : knowledgePoint;
        }

        private static List<string> BloomTerms(string bloomLevel)
        {
            return bloomLevel switch
            {
                "Remember" => new List<string> { "definition", "khái niệm", "định nghĩa", "fact", "thuật ngữ" },
                "Understand" => new List<string> { "explain", "giải thích", "ý nghĩa", "ví dụ", "mô tả" },
                "Apply" => new List<string> { "apply", "áp dụng", "thực hiện", "bài tập", "tình huống" },
                "Analyze" => new List<string> { "analyze", "phân tích", "so sánh", "nguyên nhân", "quan hệ" },
                "Evaluate" => new List<string> { "evaluate", "đánh giá", "tiêu chí", "ưu điểm", "hạn chế" },
                "Create" => new List<string> { "create", "thiết kế", "xây dựng", "đề xuất", "giải pháp" },
                _ => new List<string>()
            };
        }

        private static string TrimToSentence(string text)
        {
            var normalized = string.Join(" ", text.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
        }

        private static string BuildFeedback(List<AiGeneratedQuestion> questions)
        {
            return string.Join(Environment.NewLine, questions
                .Where(q => !string.IsNullOrWhiteSpace(q.Feedback))
                .Select(q => $"- score={q.Score}: {q.Feedback}")
                .Take(5));
        }

        private class GeneratedQuestionWrapper
        {
            public List<GeneratedQuestionDraft>? Questions { get; set; }
        }

        private class GeneratedQuestionDraft
        {
            public string QuestionName { get; set; } = string.Empty;
            public string QuestionText { get; set; } = string.Empty;
            public string Type { get; set; } = "MultipleChoice";
            public string BloomLevel { get; set; } = "Understand";
            public List<AiGeneratedChoiceDto> Choices { get; set; } = new();
            public List<string> GroundingRefs { get; set; } = new();
            public string? Feedback { get; set; }
        }

        private class EvaluatorModelResponse
        {
            public decimal? BloomAlignment { get; set; }
            public decimal? Grounding { get; set; }
            public decimal? Clarity { get; set; }
            public decimal? DistractorQuality { get; set; }
            public List<string>? Reasons { get; set; }

            public bool HasAllScores()
            {
                return BloomAlignment.HasValue
                    && Grounding.HasValue
                    && Clarity.HasValue
                    && DistractorQuality.HasValue;
            }
        }

        private record EvaluationScore(
            decimal BloomAlignment,
            decimal Grounding,
            decimal Clarity,
            decimal DistractorQuality,
            decimal FinalScore,
            List<string> Reasons);
    }
}

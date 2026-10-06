using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeDatabaseInitialization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS ""AiQuestionGenerationJobs"" (
                    ""Id"" uuid NOT NULL,
                    ""DocumentId"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""CreatedById"" uuid NOT NULL,
                    ""BloomLevel"" text NOT NULL,
                    ""QuestionType"" text NOT NULL,
                    ""QuestionCount"" integer NOT NULL,
                    ""RetrievalTopK"" integer NOT NULL DEFAULT 5,
                    ""KnowledgePoint"" character varying(500),
                    ""Status"" text NOT NULL,
                    ""ErrorMessage"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""CompletedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_AiQuestionGenerationJobs"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""LectureDocuments"" (
                    ""Id"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""UploadedById"" uuid NOT NULL,
                    ""FileName"" text NOT NULL,
                    ""ContentType"" text NOT NULL,
                    ""FilePath"" text NOT NULL,
                    ""Status"" text NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_LectureDocuments"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""AiGeneratedQuestions"" (
                    ""Id"" uuid NOT NULL,
                    ""JobId"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""QuestionName"" text NOT NULL,
                    ""QuestionText"" text NOT NULL,
                    ""Type"" text NOT NULL,
                    ""BloomLevel"" text NOT NULL,
                    ""Status"" text NOT NULL,
                    ""Score"" numeric NOT NULL,
                    ""GroundingRefsJson"" text,
                    ""ChoicesJson"" text,
                    ""Feedback"" text,
                    ""Attempt"" integer NOT NULL,
                    ""CreatedById"" uuid NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_AiGeneratedQuestions"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_AiGeneratedQuestions_AiQuestionGenerationJobs_JobId"" FOREIGN KEY (""JobId"") REFERENCES ""AiQuestionGenerationJobs"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""LectureChunks"" (
                    ""Id"" uuid NOT NULL,
                    ""DocumentId"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""Heading"" text,
                    ""Content"" text NOT NULL,
                    ""EmbeddingJson"" text,
                    ""EmbeddingModel"" text,
                    ""EmbeddingVector"" vector(768),
                    ""ChunkOrder"" integer NOT NULL,
                    ""PageNumber"" integer,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_LectureChunks"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_LectureChunks_LectureDocuments_DocumentId"" FOREIGN KEY (""DocumentId"") REFERENCES ""LectureDocuments"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""AiEvaluationResults"" (
                    ""Id"" uuid NOT NULL,
                    ""GeneratedQuestionId"" uuid NOT NULL,
                    ""BloomAlignment"" numeric NOT NULL,
                    ""Grounding"" numeric NOT NULL,
                    ""Clarity"" numeric NOT NULL,
                    ""DistractorQuality"" numeric NOT NULL,
                    ""FinalScore"" numeric NOT NULL,
                    ""ReasonsJson"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_AiEvaluationResults"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_AiEvaluationResults_AiGeneratedQuestions_GeneratedQuestionId"" FOREIGN KEY (""GeneratedQuestionId"") REFERENCES ""AiGeneratedQuestions"" (""Id"") ON DELETE CASCADE
                );

                ALTER TABLE ""LectureChunks"" ADD COLUMN IF NOT EXISTS ""EmbeddingJson"" text;
                ALTER TABLE ""LectureChunks"" ADD COLUMN IF NOT EXISTS ""EmbeddingModel"" text;
                ALTER TABLE ""LectureChunks"" ADD COLUMN IF NOT EXISTS ""EmbeddingVector"" vector(768);
                ALTER TABLE ""AiQuestionGenerationJobs"" ADD COLUMN IF NOT EXISTS ""RetrievalTopK"" integer NOT NULL DEFAULT 5;
                ALTER TABLE ""AiQuestionGenerationJobs"" ADD COLUMN IF NOT EXISTS ""KnowledgePoint"" character varying(500);

                CREATE INDEX IF NOT EXISTS ""IX_AiEvaluationResults_GeneratedQuestionId"" ON ""AiEvaluationResults"" (""GeneratedQuestionId"");
                CREATE INDEX IF NOT EXISTS ""IX_AiGeneratedQuestions_JobId_Score"" ON ""AiGeneratedQuestions"" (""JobId"", ""Score"");
                CREATE INDEX IF NOT EXISTS ""IX_LectureChunks_DocumentId_ChunkOrder"" ON ""LectureChunks"" (""DocumentId"", ""ChunkOrder"");
                CREATE INDEX IF NOT EXISTS ""IX_LectureChunks_EmbeddingVector_Hnsw""
                    ON ""LectureChunks""
                    USING hnsw (""EmbeddingVector"" vector_cosine_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_LectureChunks_EmbeddingVector_Hnsw"";");

            migrationBuilder.DropTable(
                name: "AiEvaluationResults");

            migrationBuilder.DropTable(
                name: "LectureChunks");

            migrationBuilder.DropTable(
                name: "AiGeneratedQuestions");

            migrationBuilder.DropTable(
                name: "LectureDocuments");

            migrationBuilder.DropTable(
                name: "AiQuestionGenerationJobs");
        }
    }
}

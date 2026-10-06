using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetsLearn.Infrastructure.Migrations
{
    [Migration("20261003124500_CreateCurrentSchemaBaseline")]
    [DbContext(typeof(LetsLearnContext))]
    public partial class CreateCurrentSchemaBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS ""AssignmentResponses"" (
                    ""Id"" uuid NOT NULL,
                    ""StudentId"" uuid NOT NULL,
                    ""TopicId"" uuid NOT NULL,
                    ""SubmittedAt"" timestamp with time zone,
                    ""Note"" text,
                    ""Mark"" numeric,
                    ""GradedAt"" timestamp with time zone,
                    ""GradedBy"" uuid,
                    CONSTRAINT ""PK_AssignmentResponses"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Comments"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""TopicId"" uuid NOT NULL,
                    ""Text"" text NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""ParentCommentId"" uuid,
                    ""RootCommentId"" uuid,
                    CONSTRAINT ""PK_Comments"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_Comments_Comments_ParentCommentId"" FOREIGN KEY (""ParentCommentId"") REFERENCES ""Comments"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_Comments_Comments_RootCommentId"" FOREIGN KEY (""RootCommentId"") REFERENCES ""Comments"" (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Conversations"" (
                    ""Id"" uuid NOT NULL,
                    ""User1Id"" uuid NOT NULL,
                    ""User2Id"" uuid NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_Conversations"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Courses"" (
                    ""Id"" text NOT NULL,
                    ""CreatorId"" uuid NOT NULL,
                    ""Title"" text NOT NULL,
                    ""Description"" text,
                    ""TotalJoined"" integer NOT NULL,
                    ""ImageUrl"" text,
                    ""Price"" numeric,
                    ""Category"" text,
                    ""Level"" text,
                    ""IsPublished"" boolean NOT NULL,
                    CONSTRAINT ""PK_Courses"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Notifications"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""Type"" text NOT NULL,
                    ""EntityId"" uuid NOT NULL,
                    ""ActorId"" uuid NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""ReadAt"" timestamp with time zone,
                    ""Data"" text,
                    CONSTRAINT ""PK_Notifications"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Payments"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""Amount"" numeric NOT NULL,
                    ""Description"" text,
                    ""OrderId"" text,
                    ""TransactionId"" text,
                    ""Status"" text NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""PaidAt"" timestamp with time zone,
                    CONSTRAINT ""PK_Payments"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Questions"" (
                    ""Id"" uuid NOT NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    ""DeletedAt"" timestamp with time zone,
                    ""QuestionName"" text,
                    ""QuestionText"" text,
                    ""Status"" text,
                    ""Type"" text,
                    ""DefaultMark"" numeric,
                    ""Usage"" bigint,
                    ""FeedbackOfTrue"" text,
                    ""FeedbackOfFalse"" text,
                    ""CorrectAnswer"" boolean NOT NULL,
                    ""Multiple"" boolean NOT NULL,
                    ""CreatedById"" uuid NOT NULL,
                    ""ModifiedById"" uuid,
                    ""CourseId"" text NOT NULL,
                    CONSTRAINT ""PK_Questions"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""QuizResponses"" (
                    ""Id"" uuid NOT NULL,
                    ""StudentId"" uuid NOT NULL,
                    ""TopicId"" uuid NOT NULL,
                    ""Status"" text,
                    ""StartedAt"" timestamp with time zone,
                    ""CompletedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_QuizResponses"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Users"" (
                    ""Id"" uuid NOT NULL,
                    ""Username"" text NOT NULL,
                    ""Email"" text NOT NULL,
                    ""PasswordHash"" text NOT NULL,
                    ""Avatar"" text,
                    ""IsVerified"" boolean NOT NULL,
                    ""Role"" text NOT NULL,
                    CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id"")
                );

                CREATE TABLE IF NOT EXISTS ""Messages"" (
                    ""Id"" uuid NOT NULL,
                    ""ConversationId"" uuid NOT NULL,
                    ""SenderId"" uuid NOT NULL,
                    ""Content"" text NOT NULL,
                    ""ImageUrl"" text,
                    ""FileUrl"" text,
                    ""FileName"" text,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_Messages"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_Messages_Conversations_ConversationId"" FOREIGN KEY (""ConversationId"") REFERENCES ""Conversations"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""Sections"" (
                    ""Id"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""Position"" integer,
                    ""Title"" text,
                    ""Description"" text,
                    CONSTRAINT ""PK_Sections"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_Sections_Courses_CourseId"" FOREIGN KEY (""CourseId"") REFERENCES ""Courses"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""QuestionChoices"" (
                    ""Id"" uuid NOT NULL,
                    ""QuestionId"" uuid NOT NULL,
                    ""Text"" text,
                    ""GradePercent"" numeric,
                    ""Feedback"" text,
                    CONSTRAINT ""PK_QuestionChoices"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_QuestionChoices_Questions_QuestionId"" FOREIGN KEY (""QuestionId"") REFERENCES ""Questions"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""QuizResponseAnswers"" (
                    ""Id"" uuid NOT NULL,
                    ""QuizResponseId"" uuid NOT NULL,
                    ""Question"" text,
                    ""Answer"" text,
                    ""Mark"" numeric,
                    CONSTRAINT ""PK_QuizResponseAnswers"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_QuizResponseAnswers_QuizResponses_QuizResponseId"" FOREIGN KEY (""QuizResponseId"") REFERENCES ""QuizResponses"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""Enrollments"" (
                    ""StudentId"" uuid NOT NULL,
                    ""CourseId"" text NOT NULL,
                    ""JoinDate"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_Enrollments"" PRIMARY KEY (""StudentId"", ""CourseId""),
                    CONSTRAINT ""FK_Enrollments_Courses_CourseId"" FOREIGN KEY (""CourseId"") REFERENCES ""Courses"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_Enrollments_Users_StudentId"" FOREIGN KEY (""StudentId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
                );

                CREATE TABLE IF NOT EXISTS ""RefreshTokens"" (
                    ""Id"" uuid NOT NULL,
                    ""UserId"" uuid NOT NULL,
                    ""Token"" text NOT NULL,
                    ""ExpiryDate"" timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_RefreshTokens"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_RefreshTokens_Users_UserId"" FOREIGN KEY (""UserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""Topics"" (
                    ""Id"" uuid NOT NULL,
                    ""SectionId"" uuid NOT NULL,
                    ""Title"" text,
                    ""Type"" text,
                    CONSTRAINT ""PK_Topics"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_Topics_Sections_SectionId"" FOREIGN KEY (""SectionId"") REFERENCES ""Sections"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicAssignments"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    ""Open"" timestamp with time zone,
                    ""Close"" timestamp with time zone,
                    ""MaximumFile"" integer,
                    ""MaximumFileSize"" text,
                    ""RemindToGrade"" timestamp with time zone,
                    CONSTRAINT ""PK_TopicAssignments"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicAssignments_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicFiles"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    CONSTRAINT ""PK_TopicFiles"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicFiles_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicLinks"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    ""Url"" text,
                    CONSTRAINT ""PK_TopicLinks"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicLinks_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicMeetings"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    ""Open"" timestamp with time zone,
                    ""Close"" timestamp with time zone,
                    ""MeetingLink"" text,
                    CONSTRAINT ""PK_TopicMeetings"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicMeetings_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicPages"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    ""Content"" text,
                    CONSTRAINT ""PK_TopicPages"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicPages_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicQuizzes"" (
                    ""TopicId"" uuid NOT NULL,
                    ""Description"" text,
                    ""Open"" timestamp with time zone,
                    ""Close"" timestamp with time zone,
                    ""TimeLimit"" integer,
                    ""TimeLimitUnit"" text,
                    ""GradeToPass"" numeric,
                    ""GradingMethod"" text,
                    ""AttemptAllowed"" text,
                    CONSTRAINT ""PK_TopicQuizzes"" PRIMARY KEY (""TopicId""),
                    CONSTRAINT ""FK_TopicQuizzes_Topics_TopicId"" FOREIGN KEY (""TopicId"") REFERENCES ""Topics"" (""Id"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""CloudinaryFiles"" (
                    ""Id"" uuid NOT NULL,
                    ""Name"" text,
                    ""DisplayUrl"" text,
                    ""DownloadUrl"" text,
                    ""AssignmentResponseId"" uuid,
                    ""TopicFileId"" uuid,
                    ""TopicAssignmentId"" uuid,
                    ""TopicFileTopicId"" uuid,
                    CONSTRAINT ""PK_CloudinaryFiles"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_CloudinaryFiles_AssignmentResponses_AssignmentResponseId"" FOREIGN KEY (""AssignmentResponseId"") REFERENCES ""AssignmentResponses"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_CloudinaryFiles_TopicAssignments_TopicAssignmentId"" FOREIGN KEY (""TopicAssignmentId"") REFERENCES ""TopicAssignments"" (""TopicId"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_CloudinaryFiles_TopicFiles_TopicFileId"" FOREIGN KEY (""TopicFileId"") REFERENCES ""TopicFiles"" (""TopicId"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_CloudinaryFiles_TopicFiles_TopicFileTopicId"" FOREIGN KEY (""TopicFileTopicId"") REFERENCES ""TopicFiles"" (""TopicId"")
                );

                CREATE TABLE IF NOT EXISTS ""TopicMeetingHistories"" (
                    ""Id"" uuid NOT NULL,
                    ""TopicMeetingId"" uuid NOT NULL,
                    ""StartTime"" timestamp with time zone NOT NULL,
                    ""EndTime"" timestamp with time zone,
                    ""AttendeeCount"" integer NOT NULL,
                    ""AttendanceCsvUrl"" text,
                    CONSTRAINT ""PK_TopicMeetingHistories"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_TopicMeetingHistories_TopicMeetings_TopicMeetingId"" FOREIGN KEY (""TopicMeetingId"") REFERENCES ""TopicMeetings"" (""TopicId"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicQuizQuestions"" (
                    ""Id"" uuid NOT NULL,
                    ""TopicQuizId"" uuid NOT NULL,
                    ""QuestionName"" text,
                    ""QuestionText"" text,
                    ""Type"" text,
                    ""DefaultMark"" numeric,
                    ""FeedbackOfTrue"" text,
                    ""FeedbackOfFalse"" text,
                    ""CorrectAnswer"" boolean,
                    ""Multiple"" boolean,
                    CONSTRAINT ""PK_TopicQuizQuestions"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_TopicQuizQuestions_TopicQuizzes_TopicQuizId"" FOREIGN KEY (""TopicQuizId"") REFERENCES ""TopicQuizzes"" (""TopicId"") ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS ""TopicQuizQuestionChoices"" (
                    ""Id"" uuid NOT NULL,
                    ""QuizQuestionId"" uuid NOT NULL,
                    ""Text"" text,
                    ""GradePercent"" numeric,
                    ""Feedback"" text,
                    CONSTRAINT ""PK_TopicQuizQuestionChoices"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_TopicQuizQuestionChoices_TopicQuizQuestions_QuizQuestionId"" FOREIGN KEY (""QuizQuestionId"") REFERENCES ""TopicQuizQuestions"" (""Id"") ON DELETE CASCADE
                );

                ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""ImageUrl"" text;
                ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""FileUrl"" text;
                ALTER TABLE ""Messages"" ADD COLUMN IF NOT EXISTS ""FileName"" text;
                ALTER TABLE ""TopicMeetings"" ADD COLUMN IF NOT EXISTS ""MeetingLink"" text;

                CREATE INDEX IF NOT EXISTS ""IX_CloudinaryFiles_AssignmentResponseId"" ON ""CloudinaryFiles"" (""AssignmentResponseId"");
                CREATE INDEX IF NOT EXISTS ""IX_CloudinaryFiles_TopicAssignmentId"" ON ""CloudinaryFiles"" (""TopicAssignmentId"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CloudinaryFiles_TopicFileId"" ON ""CloudinaryFiles"" (""TopicFileId"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CloudinaryFiles_TopicFileTopicId"" ON ""CloudinaryFiles"" (""TopicFileTopicId"");
                CREATE INDEX IF NOT EXISTS ""IX_Comments_ParentCommentId"" ON ""Comments"" (""ParentCommentId"");
                CREATE INDEX IF NOT EXISTS ""IX_Comments_RootCommentId"" ON ""Comments"" (""RootCommentId"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Courses_Title"" ON ""Courses"" (""Title"");
                CREATE INDEX IF NOT EXISTS ""IX_Enrollments_CourseId"" ON ""Enrollments"" (""CourseId"");
                CREATE INDEX IF NOT EXISTS ""IX_Messages_ConversationId"" ON ""Messages"" (""ConversationId"");
                CREATE INDEX IF NOT EXISTS ""IX_QuestionChoices_QuestionId"" ON ""QuestionChoices"" (""QuestionId"");
                CREATE INDEX IF NOT EXISTS ""IX_QuizResponseAnswers_QuizResponseId"" ON ""QuizResponseAnswers"" (""QuizResponseId"");
                CREATE INDEX IF NOT EXISTS ""IX_RefreshTokens_UserId"" ON ""RefreshTokens"" (""UserId"");
                CREATE INDEX IF NOT EXISTS ""IX_Sections_CourseId"" ON ""Sections"" (""CourseId"");
                CREATE INDEX IF NOT EXISTS ""IX_TopicMeetingHistories_TopicMeetingId"" ON ""TopicMeetingHistories"" (""TopicMeetingId"");
                CREATE INDEX IF NOT EXISTS ""IX_TopicQuizQuestionChoices_QuizQuestionId"" ON ""TopicQuizQuestionChoices"" (""QuizQuestionId"");
                CREATE INDEX IF NOT EXISTS ""IX_TopicQuizQuestions_TopicQuizId"" ON ""TopicQuizQuestions"" (""TopicQuizId"");
                CREATE INDEX IF NOT EXISTS ""IX_Topics_SectionId"" ON ""Topics"" (""SectionId"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email"" ON ""Users"" (""Email"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

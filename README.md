# LetsLearn

## Quiz Battle

Quiz Battle lets learners compete on questions from a published course. The API owns the battle clock, validates answers, scores results, and sends SignalR notifications; clients reload the authoritative room state from the REST API after a notification.

### Participants and question bank

- Only the teacher who owns a published course can create a room.
- A learner must be enrolled in that course to join. A room supports up to 8 participants, including its host.
- The question bank must contain enough usable, single-answer choice questions for the requested match. A usable question has at least two non-empty choices and exactly one choice graded 100%.
- Draft questions are excluded. Ready, active, and published questions are eligible; legacy questions with no status remain eligible for compatibility.
- Teachers can assign each bank question an Easy, Medium, or Hard difficulty. Existing questions default to Medium.

### Match rules

- The create-room API accepts 3–20 questions and 5–60 minutes. The current web UI creates a 10-question, 10-minute match.
- Difficulty can be Easy, Medium, Hard, or Mixed. Mixed attempts a balanced distribution across the three levels and fills any shortage from the other available levels. A specific level requires enough usable questions at that level.
- The host starts the room after at least one other player joins. The shared clock starts after a short countdown.
- Players can join for the first 5 minutes after the match starts and use the time remaining on the shared clock. The match ends at its configured deadline, or after the join window closes when all participants have answered every question. The host can end an active match early.
- Each correct answer earns 100 points; incorrect and unanswered questions earn 0. Answers are locked after submission. Ties are ranked by when the player submitted their final answer, then by user ID.
- After completion, each participant can review their own answers, the correct answers, correctness, and available feedback/explanations. The host can create one rematch using the same question snapshot and participant list.

### HTTP API

All Quiz Battle and leaderboard endpoints require authentication. The battle controller is available under both `/quiz-battles` and `/api/quiz-battles`.

| Method | Endpoint | Description |
| --- | --- | --- |
| `POST` | `/api/quiz-battles` | Create a room. Body: `{ "courseId": "...", "questionCount": 10, "durationMinutes": 10, "difficulty": "mixed" }`. |
| `POST` | `/api/quiz-battles/join/{code}` | Join with a six-digit code. |
| `POST` | `/api/quiz-battles/{id}/start` | Start as host. |
| `POST` | `/api/quiz-battles/{id}/answers` | Submit the current answer. Body: `{ "questionIndex": 0, "choiceId": "..." }`. |
| `POST` | `/api/quiz-battles/{id}/end` | End an active match early as host. |
| `POST` | `/api/quiz-battles/{id}/rematch` | Create or retrieve the host's rematch. |
| `GET` | `/api/quiz-battles/{id}` | Get the caller's room state, current question, and—after completion—results and answer review. |
| `GET` | `/api/quiz-battles/{id}/results` | Get final results after the match ends. |
| `DELETE` | `/api/quiz-battles/{id}/leave` | Leave a waiting room; the host closes it. |
| `GET` | `/api/leaderboard?period=all&courseId={id}` | Get up to 50 ranked learners. `period` is `all`, `month`, or `week`; omit `courseId` for the overall board. |
| `PATCH` | `/api/question/{questionId}/difficulty` | Set a question bank item's difficulty as its course teacher. Body: `{ "difficulty": "hard" }`. |

### Real-time updates

The authenticated SignalR hub is available at `/hubs/quiz-battle`. A client invokes `JoinBattle` with a battle ID to subscribe to that participant's room. The server publishes `PlayerJoined`, `PlayerLeft`, `BattleStarted`, `AnswerReceived`, `BattleCompleted`, `RoomClosed`, and `RematchCreated` notifications. These notifications signal that state changed; clients should call `GET /api/quiz-battles/{id}` for the authoritative state. Periodic REST sync provides recovery when a connection drops.

### Database

The API applies pending EF Core migrations during startup. The Quiz Battle schema includes migrations for rematch tracking and question/battle difficulty; older question rows are backfilled as Medium and existing battles as Mixed.

### Manual smoke check

1. Publish a course and prepare at least 10 Ready, single-answer choice questions. Assign Easy, Medium, and Hard levels if you want to exercise Mixed selection.
2. Sign in as the course teacher, create a room, then sign in as an enrolled learner and join with the room code.
3. Start the match and confirm both clients receive roster/progress updates. Submit answers from each client; answer submission should be accepted only once per question.
4. Complete or end the match. Confirm each player sees their own answer review, the correct answers, available explanations, and final ranking.
5. Start a rematch as the host and check that the prior participants can join the new waiting room. Check both the overall and course leaderboard.

using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class TopicQuizQuestionChoiceRepository : GenericRepository<TopicQuizQuestionChoice>, ITopicQuizQuestionChoiceRepository
    {
        public TopicQuizQuestionChoiceRepository(LetsLearnContext context) : base(context) { }

    }
}

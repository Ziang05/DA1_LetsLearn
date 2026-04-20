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
    public class QuestionChoiceRepository : GenericRepository<QuestionChoice>, IQuestionChoiceRepository
    {
        public QuestionChoiceRepository(LetsLearnContext context) : base(context) { }
    }
}

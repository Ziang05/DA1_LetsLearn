using LetsLearn.Core.Entities;
using LetsLearn.Core.Interfaces;
using LetsLearn.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LetsLearn.Infrastructure.Repository
{
    public class TopicLinkRepository : GenericRepository<TopicLink>, ITopicLinkRepository
    {
        public TopicLinkRepository(LetsLearnContext context) : base(context) { }
        public async Task UpdateAsync(TopicLink topic)
        {
            _context.Entry(topic).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}

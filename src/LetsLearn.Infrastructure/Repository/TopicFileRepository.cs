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
    public class TopicFileRepository : GenericRepository<TopicFile>, ITopicFileRepository
    {
        public TopicFileRepository(LetsLearnContext context) : base(context) { }
        public async Task UpdateAsync(TopicFile topic)
        {
            _context.Entry(topic).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }
    }
}

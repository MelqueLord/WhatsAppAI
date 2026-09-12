using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260912093000_AddServiceQueueInteractionReply")]
public partial class AddServiceQueueInteractionReply
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The runtime migration only needs the operations declared in the
        // companion migration file. The model snapshot remains the source of
        // truth for the current schema.
    }
}

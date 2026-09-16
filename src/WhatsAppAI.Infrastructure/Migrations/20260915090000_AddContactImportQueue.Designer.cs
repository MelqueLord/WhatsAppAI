using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WhatsAppAI.Infrastructure.Persistence;

#nullable disable

namespace WhatsAppAI.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260915090000_AddContactImportQueue")]
public partial class AddContactImportQueue
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The runtime migration declares the schema operations. The model snapshot
        // is the source of truth for the current schema.
    }
}

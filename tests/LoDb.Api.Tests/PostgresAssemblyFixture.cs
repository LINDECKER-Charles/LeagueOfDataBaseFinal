using LoDb.Testing;

// One PostgreSQL server for the whole assembly: every test creates its own database on it.
[assembly: AssemblyFixture(typeof(PostgresContainerFixture))]

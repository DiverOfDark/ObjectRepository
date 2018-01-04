# ObjectRepository
EscapeTeams In-Memory Object Database [![Build status](https://ci.appveyor.com/api/projects/status/5ofaya2rcml1v3nq?svg=true)](https://ci.appveyor.com/project/DiverOfDark/objectrepository)

This is an in-memory object database which was developed for EscapeTeams.ru site.

When it starts it will:
1) Load all entities from storage on start (currently only available storage is Azure Table);
2) Warmup all relations and build PK/Foreign indexes;
3) Periodically (by timer) remind storage to save everything.

After warmup all methods are available and it can be used.

Cool things to-do:
1) Make snapshot+eventlog storage provider 
2) Use raft algo to make it multiprocess-friendly

using System.Collections.Concurrent;

namespace Indrex;

public class Page
{
    public Int32 PageNo { get; }

    public Memory<Byte> Memory { get; }

    public Page(Int32 pageNo, Memory<Byte> memory)
    {
        PageNo = pageNo;
        Memory = memory;
    }
}

public abstract class PageManager
{
    public abstract Page Get(Int32 pageNo);
}

public class InMemoryPageManager : PageManager
{
    private readonly Int32 pageSize;

    ConcurrentDictionary<Int32, Page> pages;

    Func<Int32, Page> createPage;

    public InMemoryPageManager(Int32 pageSize)
    {
        this.pageSize = pageSize;
        this.pages = new ConcurrentDictionary<Int32, Page>();
        createPage = pageNo => new Page(pageNo, new Byte[pageSize]);
    }

    public override Page Get(Int32 pageNo)
    {
        return pages.GetOrAdd(pageNo, createPage);
    }
}

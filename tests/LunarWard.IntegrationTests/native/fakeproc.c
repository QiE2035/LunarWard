/*
 * fakeproc —— 集成测试用的"合成 wine 客户端"。
 *
 * 它做三件事，正好对应引擎依赖的三条真实信号：
 *   1. 继承 WINEPREFIX（由父进程 env 传入）—— 用于 /proc/<pid>/environ 判定
 *   2. 把指定文件 mmap 进地址空间 —— 用于 /proc/<pid>/maps 判定"镜像已加载"
 *   3. 睡眠 N 秒后退出 —— 用于 Running / 收敛 / OnGameExit 判定
 *
 * 编译：cc -O1 -o fakeproc fakeproc.c
 */
#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/mman.h>
#include <sys/stat.h>

int main(int argc, char **argv)
{
    /* argv[1] = 要 mmap 的镜像路径（可为 "-"）; argv[2] = 秒数 */
    const char *image = argc > 1 ? argv[1] : "-";
    int seconds = argc > 2 ? atoi(argv[2]) : 3;

    if (image && strcmp(image, "-") != 0)
    {
        int fd = open(image, O_RDONLY);
        if (fd >= 0)
        {
            struct stat st;
            if (fstat(fd, &st) == 0 && st.st_size > 0)
            {
                void *p = mmap(NULL, (size_t)st.st_size, PROT_READ, MAP_PRIVATE, fd, 0);
                (void)p;
            }
            close(fd);
        }
    }

    const char *marker = getenv("FAKEPROC_MARKER");
    if (marker && *marker)
    {
        FILE *f = fopen(marker, "w");
        if (f)
        {
            fprintf(f, "%d\n", (int)getpid());
            fclose(f);
        }
    }

    const char *out = getenv("FAKEPROC_STDOUT");
    if (out && *out)
    {
        FILE *f = fopen(out, "a");
        if (f)
        {
            fprintf(f, "fakeproc %s up\n", argv[0]);
            fclose(f);
        }
    }

    if (seconds > 0)
    {
        sleep((unsigned)seconds);
    }

    return 0;
}

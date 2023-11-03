// libmetadata.cpp : Defines the entry point for the application.
//

#include <stdio.h>

#include <libavformat/avformat.h>
#include <libavutil/dict.h>

int main(int argc, char** argv)
{
    AVFormatContext* fmt_ctx = NULL;
    AVDictionaryEntry* tag = NULL;
    int ret;

    const char* test_file = "/mnt/d/Downloads/Enemy Inside/Phoenix [2018-09-28]/Enemy Inside - Phoenix - Halo.mp3";
    printf("Test file: %s\n", test_file);
    av_register_all();
    if ((ret = avformat_open_input(&fmt_ctx, test_file, NULL, NULL)))
        return ret;

    while ((tag = av_dict_get(fmt_ctx->metadata, "", tag, AV_DICT_IGNORE_SUFFIX)))
        printf("%s=%s\n", tag->key, tag->value);

    for (unsigned i = 0; i < fmt_ctx->nb_streams; i++) {
        printf("Stream %d:\n", i);
        printf("    timebase=%d/%d\n", fmt_ctx->streams[i]->time_base.num, fmt_ctx->streams[i]->time_base.den);
        printf("    duration=%lld\n", fmt_ctx->streams[i]->duration);

        tag = NULL;
        while ((tag = av_dict_get(fmt_ctx->streams[i]->metadata, "", tag, AV_DICT_IGNORE_SUFFIX)))
            printf("    .%s=%s\n", tag->key, tag->value);
    }

    avformat_free_context(fmt_ctx);
    return 0;
}
// libmetadata.cpp : Defines the entry point for the application.
//

#include <stdio.h>
#include <limits>
#include <argparse/argparse.hpp>
#include <nlohmann/json.hpp>

extern "C" {
#   include <libavformat/avformat.h>
#   include <libavutil/dict.h>
}

template<typename ... Args>
std::string string_format(const std::string& format, Args ... args)
{
    int size_s = std::snprintf(nullptr, 0, format.c_str(), args ...) + 1; // Extra space for '\0'
    if (size_s <= 0) { throw std::runtime_error("Error during formatting."); }
    auto size = static_cast<size_t>(size_s);
    std::unique_ptr<char[]> buf(new char[size]);
    std::snprintf(buf.get(), size, format.c_str(), args ...);
    return std::string(buf.get(), buf.get() + size - 1); // We don't want the '\0' inside
}

double timeBaseToSeconds(int64_t duration,
                         const AVRational& time_base)
{
    return static_cast<double>(duration * time_base.num)
         / static_cast<double>(time_base.den);;
}

nlohmann::json getDict(AVDictionary* dict)
{
    nlohmann::json jsonDict;
    AVDictionaryEntry* entry = nullptr;

    while ((entry = av_dict_get(dict, "", entry, AV_DICT_IGNORE_SUFFIX)))
        jsonDict[entry->key] = entry->value;

    return jsonDict;
}

nlohmann::json getChapter(AVChapter* chapter)
{
    nlohmann::json chapterMeta;
    chapterMeta["id"] = chapter->id;

    if (chapter->start != std::numeric_limits<int64_t>::min())
        chapterMeta["start"] = timeBaseToSeconds(chapter->start, chapter->time_base);

    if (chapter->end != std::numeric_limits<int64_t>::min())
        chapterMeta["end"] = timeBaseToSeconds(chapter->end, chapter->time_base);

    //chapterMeta["start_tb"] = chapter->start;
    //chapterMeta["end_tb"] = chapter->end;
    //chapterMeta["time_base"]["num"] = chapter->time_base.num;
    //chapterMeta["time_base"]["den"] = chapter->time_base.den;

    if (chapter->metadata)
        chapterMeta["metadata"] = getDict(chapter->metadata);

    return chapterMeta;
}

nlohmann::json getProgram(AVProgram* program)
{
    nlohmann::json programMeta;
    programMeta["id"] = program->id;
    programMeta["program_num"] = program->program_num;

    if (program->nb_stream_indexes > 0) {
        programMeta["stream_indices"] = nlohmann::json::array();
        for (unsigned i = 0; i < program->nb_stream_indexes; i++)
            programMeta["stream_indices"].push_back(program->stream_index[i]);
    }

    if (program->metadata)
        programMeta["metadata"] = getDict(program->metadata);

    return programMeta;
}

const char* getCodecType(AVMediaType type)
{
    switch (type) {
        case AVMEDIA_TYPE_VIDEO: return "video";
        case AVMEDIA_TYPE_AUDIO: return "audio";
        case AVMEDIA_TYPE_DATA: return "data";
        case AVMEDIA_TYPE_SUBTITLE: return "subtitle";
        case AVMEDIA_TYPE_ATTACHMENT: return "attachment";
        default: return "unknown";
    }
}

nlohmann::json getStream(AVStream* stream)
{
    nlohmann::json streamMeta;
    streamMeta["index"] = stream->index;
    streamMeta["id"] = stream->id;
    //streamMeta["time_base"]["num"] = stream->time_base.num;
    //streamMeta["time_base"]["den"] = stream->time_base.den;

    if (stream->duration != std::numeric_limits<int64_t>::min())
        streamMeta["duration"] = timeBaseToSeconds(stream->duration, stream->time_base);
    //streamMeta["duration_tb"] = stream->duration;

    if (stream->start_time != std::numeric_limits<int64_t>::min()) {
        //streamMeta["start_time_tb"] = stream->start_time;
        streamMeta["start_time"] = timeBaseToSeconds(stream->start_time, stream->time_base);
    }
    
    if (stream->nb_frames)
        streamMeta["frames"] = stream->nb_frames;

    if (stream->sample_aspect_ratio.num != 0 && stream->sample_aspect_ratio.den != 0) {
        //streamMeta["aspect_ratio"]["num"] = stream->sample_aspect_ratio.num;
        //streamMeta["aspect_ratio"]["den"] = stream->sample_aspect_ratio.den;
        streamMeta["aspect_ratio"] = static_cast<double>(stream->sample_aspect_ratio.num) / static_cast<double>(stream->sample_aspect_ratio.den);
    }

    if (stream->avg_frame_rate.num != 0 && stream->avg_frame_rate.den != 0) {
        //streamMeta["frame_rate"]["num"] = stream->avg_frame_rate.num;
        //streamMeta["frame_rate"]["den"] = stream->avg_frame_rate.den;
        streamMeta["frame_rate"] = static_cast<double>(stream->avg_frame_rate.num) / static_cast<double>(stream->avg_frame_rate.den);
    }
    if (stream->r_frame_rate.num != 0) {
        //streamMeta["real_frame_rate"]["num"] = stream->r_frame_rate.num;
        //streamMeta["real_frame_rate"]["den"] = stream->r_frame_rate.den;
        streamMeta["real_frame_rate"] = static_cast<double>(stream->r_frame_rate.num) / static_cast<double>(stream->r_frame_rate.den);
    }

    if (stream->metadata)
        streamMeta["metadata"] = getDict(stream->metadata);

    if (stream->codec) {
        streamMeta["type"] = getCodecType(stream->codec->codec_type);
        streamMeta["codec"] = avcodec_get_name(stream->codec->codec_id);
        if (stream->codec->bit_rate != std::numeric_limits<int64_t>::min())
            streamMeta["bit_rate"] = stream->codec->bit_rate;
        if (stream->codec->width != 0)
            streamMeta["width"] = stream->codec->width;
        if (stream->codec->height != 0)
            streamMeta["height"] = stream->codec->height;
        if (stream->codec->sample_rate != 0)
            streamMeta["sample_rate"] = stream->codec->sample_rate;
        if (stream->codec->channels != 0)
            streamMeta["channels"] = stream->codec->channels;
        if (stream->codec->bits_per_coded_sample != 0)
            streamMeta["bits_per_sample"] = stream->codec->bits_per_coded_sample;
        else if (stream->codec->bits_per_raw_sample != 0)
            streamMeta["bits_per_sample"] = stream->codec->bits_per_raw_sample;
    }

    return streamMeta;
}

void doGet(argparse::ArgumentParser& args)
{
    std::string path = args.get("file");
    if (args.get<bool>("--wsl-path")) {
        std::replace(path.begin(), path.end(), '\\', '/');
        if (path.size() > 1 && path[1] == ':')
            path = string_format("/mnt/%c%s", std::tolower(path[0]), path.substr(2).c_str());
    }

    AVFormatContext* fmt_ctx = NULL;
    AVDictionaryEntry* tag = NULL;
    int ret;

    //const char* test_file = "/mnt/d/Downloads/Enemy Inside/Phoenix [2018-09-28]/Enemy Inside - Phoenix - Halo.mp3";
    if ((ret = avformat_open_input(&fmt_ctx, path.c_str(), NULL, NULL))) {
        std::cerr << "Failed to open file";
        std::exit(-2);
    }
    
    nlohmann::json result;
    result["parser"] = "avformat";
    result["url"] = fmt_ctx->url;

    if (fmt_ctx->start_time != std::numeric_limits<int64_t>::min())
        result["start_time_tb"] = fmt_ctx->start_time;
    if (fmt_ctx->start_time_realtime != std::numeric_limits<int64_t>::min())
        result["start_time_us_epoch"] = fmt_ctx->start_time_realtime;
    if (fmt_ctx->duration != std::numeric_limits<int64_t>::min())
        result["duration_tb"] = fmt_ctx->duration;
    if (fmt_ctx->bit_rate != 0)
        result["bit_rate"] = fmt_ctx->bit_rate;

    if (fmt_ctx->audio_codec_id != AV_CODEC_ID_NONE)
        result["audio_codec"] = avcodec_get_name(fmt_ctx->audio_codec_id);
    if (fmt_ctx->video_codec_id != AV_CODEC_ID_NONE)
        result["video_codec"] = avcodec_get_name(fmt_ctx->video_codec_id);
    if (fmt_ctx->subtitle_codec_id != AV_CODEC_ID_NONE)
        result["subtitle_codec"] = avcodec_get_name(fmt_ctx->subtitle_codec_id);
    if (fmt_ctx->data_codec_id != AV_CODEC_ID_NONE)
        result["data_codec"] = avcodec_get_name(fmt_ctx->data_codec_id);

    if (fmt_ctx->nb_programs > 0) {
        result["programs"] = nlohmann::json::array();
        for (unsigned i = 0; i < fmt_ctx->nb_programs; i++)
            result["programs"].push_back(getProgram(fmt_ctx->programs[i]));
    }

    if (fmt_ctx->nb_chapters > 0) {
        result["chapters"] = nlohmann::json::array();
        for (unsigned i = 0; i < fmt_ctx->nb_chapters; i++)
            result["chapters"].push_back(getChapter(fmt_ctx->chapters[i]));
    }

    if (fmt_ctx->metadata) {
        result["metadata"] = getDict(fmt_ctx->metadata);
    }

    if (fmt_ctx->nb_streams > 0) {
        result["streams"] = nlohmann::json::array();
        for (unsigned i = 0; i < fmt_ctx->nb_streams; i++) {
            result["streams"].push_back(getStream(fmt_ctx->streams[i]));
        }
    }

    avformat_free_context(fmt_ctx);
    
    std::cout << result << std::endl;
}

void doSet(argparse::ArgumentParser& args) 
{
}

int main(int argc, char** argv)
{
    argparse::ArgumentParser argParser("meta-cli");

    argparse::ArgumentParser getCmd("get");
    getCmd.add_argument("file")
        .help("File for which to read metadata.");
    getCmd.add_argument("--wsl-path")
        .default_value(false)
        .implicit_value(true);

    argparse::ArgumentParser setCmd("set");
    setCmd.add_argument("file")
        .help("File for which to write metadata.");

    setCmd.add_argument("metadataJson")
        .help("File for which to write metadata.");

    argParser.add_subparser(getCmd);
    argParser.add_subparser(setCmd);

    try {
        argParser.parse_args(argc, argv);
    }
    catch (std::runtime_error& err) {
        std::cerr << err.what() << std::endl;
        std::cerr << argParser;
        std::exit(-1);
    }

    if (argParser.is_subcommand_used("get")) {
        doGet(getCmd);
    }
    else if (argParser.is_subcommand_used("set")) {
        doSet(setCmd);
    }
    else {
        std::cerr << "No command specified!" << std::endl;
        std::cerr << argParser;
        std::exit(-1);
    }
}
// The following ifdef block is the standard way of creating macros which make exporting 
// from a DLL simpler. All files within this DLL are compiled with the FASTGALOISFIELDSNATIVE_EXPORTS
// symbol defined on the command line. This symbol should not be defined on any project
// that uses this DLL. This way any other project whose source files include this file see 
// FASTGALOISFIELDSNATIVE_API functions as being imported from a DLL, whereas this DLL sees symbols
// defined with this macro as being exported.
#ifdef FASTGALOISFIELDSNATIVE_EXPORTS
#define FASTGALOISFIELDSNATIVE_API __declspec(dllexport)
#else
#define FASTGALOISFIELDSNATIVE_API __declspec(dllimport)
#endif

static unsigned int bits = 16;
static unsigned int generator = 0x1100B;

static unsigned int count = 0;
static unsigned int limit = 0;

static unsigned short *log;
static unsigned short *antilog;

FASTGALOISFIELDSNATIVE_API void Initialize();

FASTGALOISFIELDSNATIVE_API unsigned short GetLimit();
FASTGALOISFIELDSNATIVE_API unsigned short *GetAntiLog();
FASTGALOISFIELDSNATIVE_API unsigned short GetAntiLogIndex(int index);

FASTGALOISFIELDSNATIVE_API unsigned short Multiply(unsigned short a, unsigned short b);
FASTGALOISFIELDSNATIVE_API unsigned short Divide(unsigned short a, unsigned short b);
FASTGALOISFIELDSNATIVE_API unsigned short Pow(unsigned short a, unsigned short b);
FASTGALOISFIELDSNATIVE_API unsigned short Add(unsigned short a, unsigned short b);
FASTGALOISFIELDSNATIVE_API unsigned short Minus(unsigned short a, unsigned short b);
FASTGALOISFIELDSNATIVE_API bool InternalProcess(unsigned short factor, unsigned int size, unsigned short* inputbuffer, unsigned short* outputbuffer, int startIndex, unsigned int length);


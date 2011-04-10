// FastGaloisFieldsNative.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "FastGaloisFieldsNative.h"

FASTGALOISFIELDSNATIVE_API void Initialize()
{
	count = (unsigned int)(1 << (int)bits);
	limit = count - 1;

	log = new unsigned short[count];
	antilog = new unsigned short[count];

	unsigned int b = 1;

	for (unsigned int l = 0; l < limit; l++)
	{
		log[b] = (unsigned short)l;
		antilog[l] = (unsigned short)b;

		b <<= 1;
		if ((b & count) != 0)
			b ^= generator;
	}

	log[0] = (unsigned short)limit;

	antilog[limit] = 0;
}

FASTGALOISFIELDSNATIVE_API unsigned short GetLimit()
{
	return limit;
}

FASTGALOISFIELDSNATIVE_API unsigned short *GetAntiLog()
{
	return antilog;
}

FASTGALOISFIELDSNATIVE_API unsigned short GetAntiLogIndex(int index)
{
	return antilog[index];
}

FASTGALOISFIELDSNATIVE_API unsigned short Multiply(unsigned short a, unsigned short b)
{
	if (a == 0 || b == 0)
		return 0;

	int sum = log[a] + log[b];

	if (sum >= limit)
	{
		return antilog[sum - limit];
	}
	else
	{
		return antilog[sum];
	}
}

FASTGALOISFIELDSNATIVE_API unsigned short Divide(unsigned short a, unsigned short b)
{
	if (a == 0) return 0;

	if (b == 0)
		return 0; // Division by 0!

	int sum = log[a] - log[b];
	if (sum < 0)
	{
		return antilog[sum + limit];
	}
	else
	{
		return antilog[sum];
	}
}

FASTGALOISFIELDSNATIVE_API unsigned short Pow(unsigned short a, unsigned short b)
{
	if (b == 0)
		return 1;

	if (a == 0)
		return 0;

	int sum = log[a] * b;

	sum = (int)((sum >> (int)bits) + (sum & limit));
	if (sum >= limit)
	{
		return antilog[sum - limit];
	}
	else
	{
		return antilog[sum];
	}
}

FASTGALOISFIELDSNATIVE_API unsigned short Add(unsigned short a, unsigned short b)
{
	return (unsigned short)(a ^ b);
}

FASTGALOISFIELDSNATIVE_API unsigned short Minus(unsigned short a, unsigned short b)
{
	return (unsigned short)(a ^ b);
}

FASTGALOISFIELDSNATIVE_API bool InternalProcess(unsigned short factor, unsigned int size, unsigned short* inputbuffer, unsigned short* outputbuffer, int startIndex, unsigned int length)
{
	try{

		unsigned short *pInput = (unsigned short*)inputbuffer;
		unsigned short *pOutput = (unsigned short*)(outputbuffer + (unsigned short)startIndex);

		for (int i = 0; i < size / 2; ++i)
		{
			*pOutput = Add(*pOutput, Multiply(*pInput, factor));

			++pInput;
			++pOutput;
		}

		return true;
	}
	catch(...)
	{
		return false;
	}
}


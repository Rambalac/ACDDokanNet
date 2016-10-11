// Cloud.DokanNet.IntegrationTests.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include <Windows.h>
#include "Cloud.DokanNet.IntegrationTests.h"

void TestOverlappedWrite(const wchar_t* dir)
{
	auto path = new wchar_t[1024];
	GetTempFileName(dir, L"tmp", 1, path);

	auto file = CreateFile(path, GENERIC_WRITE, 0, 0, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED, 0);

	const auto length = 1 << 20;
	auto buffer = new byte[length];
	for (int i = 0; i < length; i++)
	{
		buffer[i] = i & 255;
	}

	const int parts = 8;
	auto overlapped = new OVERLAPPED[parts];

	SetFilePointer(file, length*parts, 0, FILE_BEGIN);
	SetEndOfFile(file);

	for (int i = 0; i < parts; i++)
	{
		memset(overlapped + i, 0, sizeof(OVERLAPPED));
		overlapped[i].Offset = i*length;

		DWORD writ;
		WriteFileEx(file, buffer, length, overlapped + i, 0);
	}

	CloseHandle(file);
	delete[] overlapped;
	delete[] buffer;

	printf("Wait till file upload\r\n");
	Sleep(10000);

	buffer = new byte[length*parts];
	file = CreateFile(path, GENERIC_READ, 0, 0, OPEN_EXISTING, FILE_FLAG_SEQUENTIAL_SCAN, 0);
	DWORD red;

	DWORD pos = 0;
	do {
		ReadFile(file, buffer + pos, length*parts, &red, 0);
		pos += red;
	} while (red > 0 && pos < length*parts);

	CloseHandle(file);

	if (pos != length*parts)
	{
		printf("Read only %d(%d) bytes of %d\r\n", pos, red, length*parts);
		exit(0);
	}

	for (int i = 0; i < length*parts; i++)
	{
		byte val = (i%length) & 255;
		if (buffer[i] != val)
		{
			printf("Expected %d was %d at %d\r\n", val, buffer[i], i);
			exit(0);
		}
	}

	delete[] buffer;
	DeleteFile(path);
}

int main()
{
	//wchar_t* temp;
	//size_t tempSize;
	//_wdupenv_s(&temp, &tempSize, L"TEMP");
	//TestOverlappedWrite(temp);

	const auto path = L"F:\\Tests";
	_wmkdir(path);

	for (int i = 0; i < 10; i++)
	{
		printf("Iteration %d\r\n", i);
		TestOverlappedWrite(path);
	}

	_wrmdir(path);
	printf("All testest passed\r\n");
}
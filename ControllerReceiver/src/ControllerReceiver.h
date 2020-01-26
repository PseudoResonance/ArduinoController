#pragma once
#include <MSMotorShield.h>
#include <Arduino.h>

#define BUTTON_DPAD_UP 1
#define BUTTON_DPAD_DOWN 2
#define BUTTON_DPAD_LEFT 4
#define BUTTON_DPAD_RIGHT 8
#define BUTTON_START 16
#define BUTTON_BACK 32
#define BUTTON_LEFT_STICK 64
#define BUTTON_RIGHT_STICK 128
#define BUTTON_LEFT_BUMPER 256
#define BUTTON_RIGHT_BUMPER 512
#define BUTTON_A 4096
#define BUTTON_B 8192
#define BUTTON_X 16384
#define BUTTON_Y 32768

void init(long baudRate = 115200);
double getLeftX();
double getLeftY();
double getRightX();
double getRightY();
double getLeftTrigger();
double getRightTrigger();
MS_DCMotor* getMotor1();
MS_DCMotor* getMotor2();
MS_DCMotor* getMotor3();
MS_DCMotor* getMotor4();
bool isButtonPressed(uint16_t flag);
void setMotor(MS_DCMotor* motor, uint8_t speed, bool reverse);
void halt();
bool runLoop();
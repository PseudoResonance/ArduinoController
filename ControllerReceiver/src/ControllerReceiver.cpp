#include "ControllerReceiver.h"
#include <MSMotorShield.h>
#include <Arduino.h>

#define SERIAL_TIMEOUT 50
#define SAFETY_TIMEOUT 250

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

class Controller {
public:
	union doubleArray {
		byte array[4];
		double num;
	} leftX;
	union doubleArray leftY;
	union doubleArray rightX;
	union doubleArray rightY;
	union doubleArray leftTrigger;
	union doubleArray rightTrigger;
	union shortArray {
		byte array[2];
		uint16_t num;
	} buttonMap;
};

MS_DCMotor motor1(1);
MS_DCMotor motor2(2);
MS_DCMotor motor3(3);
MS_DCMotor motor4(4);

Controller controller;

void init(long baudRate = 115200) {
	Serial.begin(baudRate);
	pinMode(LED_BUILTIN, OUTPUT);
	digitalWrite(LED_BUILTIN, LOW);
	motor1.run(RELEASE);
	motor2.run(RELEASE);
	motor3.run(RELEASE);
	motor4.run(RELEASE);
}

double getLeftX() {
	return controller.leftX.num;
}

double getLeftY() {
	return controller.leftY.num;
}

double getRightX() {
	return controller.rightX.num;
}

double getRightY() {
	return controller.rightY.num;
}

double getLeftTrigger() {
	return controller.leftTrigger.num;
}

double getRightTrigger() {
	return controller.rightTrigger.num;
}

MS_DCMotor* getMotor1() {
	return &motor1;
}

MS_DCMotor* getMotor2() {
	return &motor2;
}

MS_DCMotor* getMotor3() {
	return &motor3;
}

MS_DCMotor* getMotor4() {
	return &motor4;
}

bool isButtonPressed(uint16_t flag) {
	if ((controller.buttonMap.num & flag) == flag) {
		return true;
	}
	return false;
}

void setMotor(MS_DCMotor* motor, uint8_t speed, bool reverse) {
	if (speed > 0) {
		if (!reverse)
			motor->run(FORWARD);
		else
			motor->run(BACKWARD);
		motor->setSpeed(speed);
	}
	else {
		motor->run(BRAKE);
		motor->setSpeed(0);
	}
}

void halt() {
	motor1.setSpeed(0);
	motor2.setSpeed(0);
	motor3.setSpeed(0);
	motor4.setSpeed(0);
	motor1.run(BRAKE);
	motor2.run(BRAKE);
	motor3.run(BRAKE);
	motor4.run(BRAKE);
	digitalWrite(LED_BUILTIN, LOW);
}

byte last;
uint16_t charsReceived;
bool receiving, run = false;

unsigned long lastRec, startRec;

bool runLoop() {
	if (Serial.available() > 0) {
		byte in = Serial.read();
		if (receiving) {
			uint8_t i = charsReceived % 4;
			if (charsReceived < 4) {
				controller.leftX.array[i] = in;
			}
			else if (charsReceived < 8) {
				controller.leftY.array[i] = in;
			}
			else if (charsReceived < 12) {
				controller.rightX.array[i] = in;
			}
			else if (charsReceived < 16) {
				controller.rightY.array[i] = in;
			}
			else if (charsReceived < 20) {
				controller.leftTrigger.array[i] = in;
			}
			else if (charsReceived < 24) {
				controller.rightTrigger.array[i] = in;
			}
			else if (charsReceived < 26) {
				controller.buttonMap.array[i] = in;
			}
			else {
				receiving = false;
				run = true;
				digitalWrite(LED_BUILTIN, HIGH);
				lastRec = millis();
			}
		}
		else {
			if (in == 114 && last == 67) {
				charsReceived = 0;
				receiving = true;
				last = 0;
				startRec = millis();
			}
			else {
				if (millis() - lastRec > SAFETY_TIMEOUT) {
					halt();
				}
				last = in;
			}
		}
	}
	else {
		if (receiving && millis() - startRec > SERIAL_TIMEOUT) {
			receiving = false;
			last = 0;
		}
	}
	if (run) {
		run = false;
		return true;
	}
	return false;
}
#include <AFMotor.h>

#define LEFT_MOTOR 2
#define RIGHT_MOTOR 1

#define SERIAL_TIMEOUT 100
#define SAFETY_TIMEOUT 500

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

AF_DCMotor motorLeft(LEFT_MOTOR);
AF_DCMotor motorRight(RIGHT_MOTOR);

void setup() {
	Serial.begin(9600);
	pinMode(LED_BUILTIN, OUTPUT);
	digitalWrite(LED_BUILTIN, LOW);
	motorLeft.run(RELEASE);
	motorRight.run(RELEASE);
}

void setMotor(AF_DCMotor* motor, uint8_t speed, bool reverse) {
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

bool testFlag(uint32_t test, uint16_t flag) {
	if ((test & flag) == flag) {
		return true;
	}
	return false;
}

void runInput() {
	setMotor(&motorLeft, (uint8_t)round(255 * leftY.num), leftY.num < 0);
	setMotor(&motorRight, (uint8_t)round(255 * rightY.num), leftY.num < 0);
	digitalWrite(LED_BUILTIN, HIGH);
}

void halt() {
	motorLeft.setSpeed(0);
	motorRight.setSpeed(0);
	motorLeft.run(BRAKE);
	motorRight.run(BRAKE);
	digitalWrite(LED_BUILTIN, LOW);
}

byte last;
uint16_t charsReceived;
bool receiving = false;

unsigned long lastRec;
unsigned long startRec;

void loop() {
	if (Serial.available() > 0) {
		byte in = Serial.read();
		if (receiving) {
			uint8_t i = charsReceived % 4;
			if (charsReceived < 4) {
				leftX.array[i] = in;
			}
			else if (charsReceived < 8) {
				leftY.array[i] = in;
			}
			else if (charsReceived < 12) {
				rightX.array[i] = in;
			}
			else if (charsReceived < 16) {
				rightY.array[i] = in;
			}
			else if (charsReceived < 20) {
				leftTrigger.array[i] = in;
			}
			else if (charsReceived < 24) {
				rightTrigger.array[i] = in;
			}
			else if (charsReceived < 26) {
				buttonMap.array[i] = in;
			}
			else {
				receiving = false;
				runInput();
				lastRec = millis();
			}
			charsReceived++;
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
}

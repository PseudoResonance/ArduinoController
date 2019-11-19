#include <MSMotorShield.h>

#define LEFT_MOTOR 2
#define RIGHT_MOTOR 1

#define BAUD_RATE 115200

#define SERIAL_TIMEOUT 100
#define SAFETY_TIMEOUT 500

#define MAX_SPEED 128

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

MS_DCMotor motorLeft(LEFT_MOTOR);
MS_DCMotor motorRight(RIGHT_MOTOR);

void setup() {
	Serial.begin(BAUD_RATE);
	pinMode(LED_BUILTIN, OUTPUT);
	digitalWrite(LED_BUILTIN, LOW);
	motorLeft.run(RELEASE);
	motorRight.run(RELEASE);
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

bool testFlag(uint32_t test, uint16_t flag) {
	if ((test & flag) == flag) {
		return true;
	}
	return false;
}

double speed, rotation, test, maxSpeed, leftSpeed, rightSpeed;

void runInput() {
	if (!testFlag(buttonMap.num, BUTTON_A)) {
		speed = rightTrigger.num - leftTrigger.num;
		rotation = leftX.num;
		speed = (speed * speed) * (speed > 0 ? 1 : -1);
		rotation = (rotation * rotation) * (rotation > 0 ? 1 : -1);

		test = max(abs(speed), abs(rotation));
		maxSpeed = (test * test) * (test > 0 ? 1 : -1);

		if (speed >= 0) {
			if (rotation >= 0) {
				leftSpeed = maxSpeed;
				rightSpeed = speed - rotation;
			}
			else {
				leftSpeed = speed + rotation;
				rightSpeed = maxSpeed;
			}
		}
		else {
			if (rotation >= 0) {
				leftSpeed = speed + rotation;
				rightSpeed = -maxSpeed;
			}
			else {
				leftSpeed = -maxSpeed;
				rightSpeed = speed - rotation;
			}
		}

		setMotor(&motorLeft, (uint8_t)round(abs(leftSpeed) * MAX_SPEED), leftSpeed < 0);
		setMotor(&motorRight, (uint8_t)round(abs(rightSpeed) * MAX_SPEED), rightSpeed < 0);
		digitalWrite(LED_BUILTIN, HIGH);
	}
	else {
		setMotor(&motorLeft, 0, false);
		setMotor(&motorRight, 0, false);
	}
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

unsigned long lastRec, startRec;

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
